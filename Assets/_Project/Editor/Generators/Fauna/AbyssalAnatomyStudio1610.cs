#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Hecton8.EditorTools.Generators.Fauna
{
    internal enum FaunaRigPreset1610 : byte
    {
        SmallFish = 0,
        MediumPredator = 1,
        Leviathan = 2,
        VatSwarm = 3
    }

    internal enum FaunaLocomotionStyle1610 : byte
    {
        SerpentineSwimmer = 0,
        PectoralPaddler = 1,
        TentacleCrawler = 2,
        VatSwarm = 3,
        Unknown = 255
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal struct FaunaMeshAxisDTO1610
    {
        [FieldOffset(0)] public float3 Center;
        [FieldOffset(12)] public float Length;
        [FieldOffset(16)] public float3 Axis;
        [FieldOffset(28)] public uint AxisIndex;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct FaunaSpineIkConfigDTO1610
    {
        [FieldOffset(0)] public uint CreatureHash;
        [FieldOffset(4)] public uint Flags;
        [FieldOffset(8)] public int BoneStart;
        [FieldOffset(12)] public int BoneCount;
        [FieldOffset(16)] public int SpineBoneCount;
        [FieldOffset(20)] public int SecondaryBoneCount;
        [FieldOffset(24)] public float SegmentLengthMeters;
        [FieldOffset(28)] public float MaxBendRadians;
        [FieldOffset(32)] public float BodyRadiusMeters;
        [FieldOffset(36)] public float GlobalQualityWeight;
        [FieldOffset(40)] public float3 AxisLocal;
        [FieldOffset(52)] public uint MetadataVersion;
        [FieldOffset(56)] public uint _pad0;
        [FieldOffset(60)] public uint _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal struct FaunaSpineBoneDTO1610
    {
        [FieldOffset(0)] public float3 LocalOffset;
        [FieldOffset(12)] public float SegmentLengthMeters;
        [FieldOffset(16)] public float MaxBendRadians;
        [FieldOffset(20)] public int ParentIndex;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal struct FaunaVertexWeightAuditDTO1610
    {
        [FieldOffset(0)] public float WeightSum;
        [FieldOffset(4)] public float NearestDistanceSq;
        [FieldOffset(8)] public int PrimaryBone;
        [FieldOffset(12)] public uint Flags;
        [FieldOffset(16)] public uint _pad0;
        [FieldOffset(20)] public uint _pad1;
        [FieldOffset(24)] public uint _pad2;
        [FieldOffset(28)] public uint _pad3;
    }

    internal struct FaunaRigMetrics1610
    {
        public int SourceVertexCount;
        public int SourceTriangleCount;
        public int BoneCount;
        public int SpineBoneCount;
        public int SecondaryBoneCount;
        public int IsolatedVertexCount;
        public int WeightNormalizationFailures;
        public int VatVertexCount;
        public int VatFrameCount;
        public double SkinningMilliseconds;
        public double VatMilliseconds;
        public double TotalMilliseconds;
        public float MaxVatPrecisionError;
        public ulong MeshHash;
        public ulong WeightHash;
        public ulong VatHash;
    }

    internal readonly struct FaunaRigOutput1610
    {
        public readonly string MeshAssetPath;
        public readonly string VatAssetPath;
        public readonly string PrefabPath;
        public readonly string MetadataPath;
        public readonly FaunaRigMetrics1610 Metrics;

        public FaunaRigOutput1610(string meshAssetPath, string vatAssetPath, string prefabPath, string metadataPath, in FaunaRigMetrics1610 metrics)
        {
            MeshAssetPath = meshAssetPath;
            VatAssetPath = vatAssetPath;
            PrefabPath = prefabPath;
            MetadataPath = metadataPath;
            Metrics = metrics;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard, OptimizeFor = OptimizeFor.Performance)]
    internal struct CalculateVertexWeightsJob1610 : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<float3> Vertices;
        [ReadOnly, NoAlias] public NativeArray<float4x4> BoneSegments;
        [NoAlias] public NativeArray<BoneWeight1> Weights;
        [NoAlias] public NativeArray<FaunaVertexWeightAuditDTO1610> Audits;
        public float MaxInfluenceDistanceMeters;
        public float FalloffPower;

        public void Execute(int index)
        {
            if (!Vertices.IsCreated || !BoneSegments.IsCreated || !Weights.IsCreated || (uint)index >= (uint)Vertices.Length)
                return;

            float3 vertex = Vertices[index];
            float best0 = -1f;
            float best1 = -1f;
            float best2 = -1f;
            float best3 = -1f;
            int bone0 = 0;
            int bone1 = 0;
            int bone2 = 0;
            int bone3 = 0;
            float nearestDistanceSq = float.MaxValue;

            int segmentCount = BoneSegments.Length;
            for (int i = 0; i < segmentCount; i++)
            {
                float4x4 segment = BoneSegments[i];
                float3 start = segment.c0.xyz;
                float3 end = segment.c1.xyz;
                int boneIndex = (int)math.round(segment.c2.x);
                float distSq = DistanceSqToSegment(vertex, start, end);
                nearestDistanceSq = math.min(nearestDistanceSq, distSq);
                float influence = math.rcp(math.max(0.000001f, distSq));
                float falloff = math.max(0.25f, math.select(2f, FalloffPower, math.isfinite(FalloffPower)));
                influence = math.pow(influence, falloff * 0.5f);

                if (influence > best0)
                {
                    best3 = best2;
                    bone3 = bone2;
                    best2 = best1;
                    bone2 = bone1;
                    best1 = best0;
                    bone1 = bone0;
                    best0 = influence;
                    bone0 = boneIndex;
                }
                else if (influence > best1)
                {
                    best3 = best2;
                    bone3 = bone2;
                    best2 = best1;
                    bone2 = bone1;
                    best1 = influence;
                    bone1 = boneIndex;
                }
                else if (influence > best2)
                {
                    best3 = best2;
                    bone3 = bone2;
                    best2 = influence;
                    bone2 = boneIndex;
                }
                else if (influence > best3)
                {
                    best3 = influence;
                    bone3 = boneIndex;
                }
            }

            float maxDistanceSq = MaxInfluenceDistanceMeters > 0f
                ? MaxInfluenceDistanceMeters * MaxInfluenceDistanceMeters
                : float.MaxValue;
            bool isolated = nearestDistanceSq > maxDistanceSq || best0 <= 0f || !math.isfinite(best0);
            float w0;
            float w1;
            float w2;
            float w3;
            uint flags = 0u;
            if (isolated)
            {
                w0 = 1f;
                w1 = 0f;
                w2 = 0f;
                w3 = 0f;
                bone0 = 0;
                bone1 = 0;
                bone2 = 0;
                bone3 = 0;
                flags |= 1u;
            }
            else
            {
                best1 = math.max(0f, best1);
                best2 = math.max(0f, best2);
                best3 = math.max(0f, best3);
                float sum = best0 + best1 + best2 + best3;
                if (!math.isfinite(sum) || sum <= 0.000001f)
                {
                    w0 = 1f;
                    w1 = 0f;
                    w2 = 0f;
                    w3 = 0f;
                    bone0 = 0;
                    bone1 = 0;
                    bone2 = 0;
                    bone3 = 0;
                    flags |= 2u;
                }
                else
                {
                    float inv = math.rcp(sum);
                    w0 = math.saturate(best0 * inv);
                    w1 = math.saturate(best1 * inv);
                    w2 = math.saturate(best2 * inv);
                    w3 = math.saturate(1f - (w0 + w1 + w2));
                }
            }

            int weightStart = index * 4;
            Weights[weightStart] = CreateWeight(bone0, w0);
            Weights[weightStart + 1] = CreateWeight(bone1, w1);
            Weights[weightStart + 2] = CreateWeight(bone2, w2);
            Weights[weightStart + 3] = CreateWeight(bone3, w3);

            if (Audits.IsCreated && (uint)index < (uint)Audits.Length)
            {
                FaunaVertexWeightAuditDTO1610 audit = default;
                audit.WeightSum = w0 + w1 + w2 + w3;
                audit.NearestDistanceSq = nearestDistanceSq;
                audit.PrimaryBone = bone0;
                audit.Flags = flags;
                Audits[index] = audit;
            }
        }

        private static BoneWeight1 CreateWeight(int boneIndex, float weight)
        {
            BoneWeight1 value = default;
            value.boneIndex = math.max(0, boneIndex);
            value.weight = math.saturate(math.select(0f, weight, math.isfinite(weight)));
            return value;
        }

        private static float DistanceSqToSegment(float3 point, float3 start, float3 end)
        {
            float3 ab = end - start;
            float denom = math.max(0.000001f, math.lengthsq(ab));
            float t = math.saturate(math.dot(point - start, ab) * math.rcp(denom));
            float3 closest = start + ab * t;
            return math.lengthsq(point - closest);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard, OptimizeFor = OptimizeFor.Performance)]
    internal struct BakeSwarmVatJob1610 : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<float3> Vertices;
        [NoAlias] public NativeArray<float4> OutputPixels;
        public int VertexCount;
        public int FrameCount;
        public float3 AxisStart;
        public float3 AxisDirection;
        public float3 SideDirection;
        public float LengthMeters;
        public float AmplitudeMeters;
        public float WaveCycles;
        public float GlobalQualityWeight;

        public void Execute(int index)
        {
            if (!Vertices.IsCreated || !OutputPixels.IsCreated || VertexCount <= 0 || FrameCount <= 0 || (uint)index >= (uint)OutputPixels.Length)
                return;

            int vertexIndex = index % VertexCount;
            int frameIndex = index / VertexCount;
            float3 vertex = Vertices[vertexIndex];
            float length = math.max(0.0001f, LengthMeters);
            float3 axis = math.normalizesafe(AxisDirection, new float3(0f, 0f, 1f));
            float t = math.saturate(math.dot(vertex - AxisStart, axis) * math.rcp(length));
            float frame01 = (frameIndex + 0.5f) * math.rcp(math.max(1, FrameCount));
            float quality = Smooth01(math.saturate(math.select(1f, GlobalQualityWeight, math.isfinite(GlobalQualityWeight))));
            float envelope = math.sin(t * math.PI);
            float phase = frame01 * math.PI * 2f - t * math.PI * 2f * math.max(0.25f, WaveCycles);
            float primary = math.sin(phase);
            float harmonic = math.sin(phase * 2.13f + t * 1.91f) * (0.18f * quality);
            float amplitude = math.max(0f, AmplitudeMeters) * math.max(0f, envelope) * math.lerp(0.55f, 1.15f, quality);
            float3 side = math.normalizesafe(SideDirection, new float3(1f, 0f, 0f));
            float3 offset = side * ((primary + harmonic) * amplitude);
            OutputPixels[index] = new float4(Sanitize(offset), 1f);
        }

        private static float Smooth01(float value)
        {
            float x = math.saturate(value);
            return x * x * (3f - 2f * x);
        }

        private static float3 Sanitize(float3 value)
        {
            return math.all(math.isfinite(value)) ? value : float3.zero;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard, OptimizeFor = OptimizeFor.Performance)]
    internal struct GenerateMockFaunaVerticesJob1610 : IJobParallelFor
    {
        [NoAlias] public NativeArray<float3> Vertices;
        public float LengthMeters;
        public float RadiusMeters;

        public void Execute(int index)
        {
            if (!Vertices.IsCreated || (uint)index >= (uint)Vertices.Length)
                return;

            int ring = index & 31;
            int slice = index >> 5;
            float z = slice * (LengthMeters * math.rcp(math.max(1, (Vertices.Length >> 5) - 1))) - LengthMeters * 0.5f;
            float angle = ring * (math.PI * 2f / 32f);
            float taper = math.sin(math.saturate((z / math.max(0.001f, LengthMeters)) + 0.5f) * math.PI);
            float radius = math.max(0.001f, RadiusMeters) * math.max(0.08f, taper);
            Vertices[index] = new float3(math.cos(angle) * radius, math.sin(angle) * radius, z);
        }
    }

    internal static class FaunaOfflineRigger1610
    {
        public const string RawInputFolder = "Assets/_Project/Art/Fauna/Raw";
        public const string MeshOutputRoot = "Assets/_Project/Art/Generated/Fauna/Rigged1610";
        public const string VatOutputRoot = "Assets/_Project/Art/Generated/Fauna/VAT1610";
        public const string SpineMetadataOutputRoot = "Assets/_Project/Data/Fauna/Rigs1610";
        public const string PrefabOutputRoot = "Assets/_Project/Prefabs/Nature/Fauna/Rigged1610";
        private const uint SpineMetadataVersion = 16100001u;
        private const uint GeneratedRigMagicH8lr = 0x524C3848u;
        private const int GeneratedRigHeaderBytes = 16;
        private const int GeneratedRigRowBytes = 16;
        private const int VatBytesPerPixel = 16;
        private const int MaxCompactVatBytes = 32 * 1024 * 1024;
        private const int MinRuntimeSpineIkSegments = 8;
        private const int MaxRuntimeLeviathanIkSegments = 20;
        private const int MaxSmallFishBones = 4;
        private const int MaxMediumPredatorBones = 24;
        private const int MaxLeviathanBones = 96;

        public static void AnalyzeRawFaunaMeshes()
        {
            Debug.Log(BuildRawMeshLedgerSummary());
        }

        public static string BuildRawMeshLedgerSummary()
        {
            StringBuilder builder = new StringBuilder(8192);
            string absoluteRoot = Path.Combine(Directory.GetCurrentDirectory(), RawInputFolder.Replace('/', Path.DirectorySeparatorChar));
            bool exists = Directory.Exists(absoluteRoot);
            builder.Append("[FaunaRigger1610] Raw fauna scan. folder=");
            builder.Append(RawInputFolder);
            builder.Append(" exists=");
            builder.Append(exists ? "true" : "false");

            int written = 0;
            if (exists)
            {
                string[] files = Directory.GetFiles(absoluteRoot, "*.*", SearchOption.AllDirectories);
                Array.Sort(files, StringComparer.Ordinal);
                for (int i = 0; i < files.Length; i++)
                {
                    string extension = Path.GetExtension(files[i]);
                    if (!IsRawMeshExtension(extension))
                        continue;

                    string assetPath = ToAssetPath(files[i]);
                    if (string.IsNullOrEmpty(assetPath))
                        continue;

                    if (TryAnalyzeMeshAsset(assetPath, out string meshSummary))
                    {
                        builder.Append('\n');
                        builder.Append(meshSummary);
                        written++;
                    }
                }
            }

            builder.Append('\n');
            builder.Append("[FaunaRigger1610] creatureCount=");
            builder.Append(written.ToString(CultureInfo.InvariantCulture));
            builder.Append(" status=");
            builder.Append(written > 0 ? "STATIC_SCAN_READY" : "NO_RAW_FAUNA_MESH_INPUTS");
            return builder.ToString();
        }

        public static bool TryRigAndBakeMesh(
            Mesh sourceMesh,
            Material sourceMaterial,
            FaunaRigPreset1610 preset,
            int requestedBoneCount,
            float globalQualityWeight,
            int vatFrameCount,
            string assetToken,
            out FaunaRigOutput1610 output)
        {
            output = default;
            if (sourceMesh == null)
            {
                Debug.LogError("[FaunaRigger1610] No source mesh selected.");
                return false;
            }

            if (!sourceMesh.isReadable)
            {
                Debug.LogError("[FaunaRigger1610] Source mesh is not readable. Enable Read/Write in import settings.");
                return false;
            }

            if (sourceMesh.vertexCount <= 2)
            {
                Debug.LogError("[FaunaRigger1610] Source mesh has fewer than three vertices.");
                return false;
            }

            EnsureFolder(MeshOutputRoot);
            EnsureFolder(VatOutputRoot);
            EnsureFolder(SpineMetadataOutputRoot);
            EnsureFolder(PrefabOutputRoot);

            string safeToken = SanitizeAssetToken(string.IsNullOrEmpty(assetToken) ? sourceMesh.name : assetToken);
            Stopwatch total = Stopwatch.StartNew();
            if (preset == FaunaRigPreset1610.VatSwarm)
                return TryBakeVatSwarmPrefab(sourceMesh, sourceMaterial, safeToken, math.max(1, vatFrameCount), globalQualityWeight, total, out output);

            return TryBakeSkinnedPrefab(sourceMesh, sourceMaterial, preset, requestedBoneCount, globalQualityWeight, safeToken, total, out output);
        }

        public static bool RunMockMillionVertexSkinningFuzzer()
        {
            const int vertexCount = 1048576;
            NativeArray<float3> vertices = default;
            NativeArray<float4x4> segments = default;
            NativeArray<BoneWeight1> weights = default;
            NativeArray<FaunaVertexWeightAuditDTO1610> audits = default;
            try
            {
                vertices = new NativeArray<float3>(vertexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                segments = new NativeArray<float4x4>(24, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                weights = new NativeArray<BoneWeight1>(vertexCount * 4, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                audits = new NativeArray<FaunaVertexWeightAuditDTO1610>(vertexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

                JobHandle seedHandle = new GenerateMockFaunaVerticesJob1610
                {
                    Vertices = vertices,
                    LengthMeters = 48f,
                    RadiusMeters = 1.4f
                }.Schedule(vertexCount, 128);
                seedHandle.Complete();

                for (int i = 0; i < segments.Length; i++)
                {
                    float z0 = math.lerp(-24f, 24f, i / (float)segments.Length);
                    float z1 = math.lerp(-24f, 24f, (i + 1) / (float)segments.Length);
                    segments[i] = EncodeBoneSegment(new float3(0f, 0f, z0), new float3(0f, 0f, z1), i);
                }

                Stopwatch stopwatch = Stopwatch.StartNew();
                JobHandle handle = new CalculateVertexWeightsJob1610
                {
                    Vertices = vertices,
                    BoneSegments = segments,
                    Weights = weights,
                    Audits = audits,
                    MaxInfluenceDistanceMeters = 12f,
                    FalloffPower = 2f
                }.Schedule(vertexCount, 128);
                handle.Complete();
                stopwatch.Stop();

                int failures = CountWeightFailures(audits, 0.0001f, out int isolated);
                if (failures != 0 || isolated != 0)
                {
                    Debug.LogError("[FaunaRigger1610] 1M fuzzer failed. normalizationFailures=" + failures + " isolated=" + isolated);
                    return false;
                }

                if (stopwatch.Elapsed.TotalMilliseconds > 500.0)
                {
                    Debug.LogError("[FaunaRigger1610] 1M fuzzer exceeded 500ms static target. ms=" + stopwatch.Elapsed.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture));
                    return false;
                }

                Debug.Log("[FaunaRigger1610] 1M fuzzer completed. vertices=1048576 ms=" + stopwatch.Elapsed.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture));
                return true;
            }
            finally
            {
                if (audits.IsCreated) audits.Dispose();
                if (weights.IsCreated) weights.Dispose();
                if (segments.IsCreated) segments.Dispose();
                if (vertices.IsCreated) vertices.Dispose();
            }
        }

        public static bool RunVatPrecisionAssertion()
        {
            const float tolerance = 0.001f;
            if (!SystemInfo.SupportsTextureFormat(TextureFormat.RGBAFloat))
            {
                Debug.LogError("[FaunaRigger1610] VAT precision assertion rejected. TextureFormat.RGBAFloat is unsupported by this graphics backend.");
                return false;
            }

            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true);
            try
            {
                Color source = new Color(0.1234567f, -0.2345678f, 0.3456789f, 1f);
                texture.SetPixel(0, 0, source);
                texture.Apply(false, false);
                Color decoded = texture.GetPixel(0, 0);
                float error = math.max(math.abs(decoded.r - source.r), math.max(math.abs(decoded.g - source.g), math.abs(decoded.b - source.b)));
                if (error >= tolerance)
                {
                    Debug.LogError("[FaunaRigger1610] VAT precision assertion failed. error=" + error.ToString("F6", CultureInfo.InvariantCulture));
                    return false;
                }

                Debug.Log("[FaunaRigger1610] VAT precision assertion passed. error=" + error.ToString("F6", CultureInfo.InvariantCulture));
                return true;
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        public static bool RunBoneLimitComplianceAudit()
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabOutputRoot });
            int failureCount = 0;
            int skinnedRendererCount = 0;
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    continue;

                SkinnedMeshRenderer[] renderers = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                {
                    SkinnedMeshRenderer renderer = renderers[rendererIndex];
                    skinnedRendererCount++;
                    int boneCount = renderer.bones != null ? renderer.bones.Length : 0;
                    int limit = ResolveBoneLimitFromName(path);
                    if (boneCount > limit)
                    {
                        Debug.LogError("[FaunaRigger1610] Bone limit violation. path=" + path + " bones=" + boneCount + " limit=" + limit);
                        failureCount++;
                    }
                }
            }

            if (guids.Length == 0 || skinnedRendererCount == 0)
            {
                Debug.LogWarning("[FaunaRigger1610] Bone limit audit found no generated skinned fauna prefabs. STATUS=NO_OUTPUT_TO_VERIFY.");
                return false;
            }

            if (failureCount == 0)
                Debug.Log("[FaunaRigger1610] Bone limit compliance audit passed for generated prefabs. STATUS=PENDING UNITY IMPORT VERIFICATION.");
            return failureCount == 0;
        }

        public static void LogFinalMetricSummary(in FaunaRigOutput1610 output)
        {
            Debug.Log("[FaunaRigger1610] Bake metrics. prefab=" + output.PrefabPath +
                      " mesh=" + output.MeshAssetPath +
                      " vertices=" + output.Metrics.SourceVertexCount.ToString(CultureInfo.InvariantCulture) +
                      " bones=" + output.Metrics.BoneCount.ToString(CultureInfo.InvariantCulture) +
                      " isolated=" + output.Metrics.IsolatedVertexCount.ToString(CultureInfo.InvariantCulture) +
                      " weightFailures=" + output.Metrics.WeightNormalizationFailures.ToString(CultureInfo.InvariantCulture) +
                      " skinMs=" + output.Metrics.SkinningMilliseconds.ToString("F3", CultureInfo.InvariantCulture) +
                      " vatMs=" + output.Metrics.VatMilliseconds.ToString("F3", CultureInfo.InvariantCulture) +
                      " sourceSha256=" + ComputeSha256ForGeneratorFiles());
        }

        private static bool TryBakeSkinnedPrefab(
            Mesh sourceMesh,
            Material sourceMaterial,
            FaunaRigPreset1610 preset,
            int requestedBoneCount,
            float globalQualityWeight,
            string safeToken,
            Stopwatch totalStopwatch,
            out FaunaRigOutput1610 output)
        {
            output = default;
            List<Vector3> vertexList = new List<Vector3>(sourceMesh.vertexCount); // COLD ALLOC: List<Vector3>[source vertex count] - editor mesh extraction scratch - owner: FaunaOfflineRigger1610
            sourceMesh.GetVertices(vertexList);
            int vertexCount = vertexList.Count;
            int triangleCount = CountMeshTriangles(sourceMesh);
            int boneLimit = ResolveBoneLimit(preset);
            int minimumBoneCount = ResolveMinimumSkinnedBoneCount(preset);
            int boneCount = math.clamp(requestedBoneCount <= 0 ? boneLimit : requestedBoneCount, minimumBoneCount, boneLimit);
            FaunaMeshAxisDTO1610 axis = AnalyzeAxis(sourceMesh.bounds);
            int spineCount = ResolveSpineBoneCount(preset, boneCount);
            int secondaryCount = math.max(0, boneCount - spineCount);
            bool requiresRuntimeSpineMetadata = RequiresRuntimeSpineMetadata(preset);
            string presetToken = preset.ToString();

            GameObject root = new GameObject("GEN_FaunaRig1610_" + presetToken + "_" + safeToken);
            Transform[] bones = null;
            Matrix4x4[] bindposes = null;
            NativeArray<float3> vertices = default;
            NativeArray<float4x4> segments = default;
            NativeArray<byte> bonesPerVertex = default;
            NativeArray<BoneWeight1> weights = default;
            NativeArray<FaunaVertexWeightAuditDTO1610> audits = default;
            try
            {
                bones = CreateBoneHierarchy(root.transform, axis, sourceMesh.bounds, spineCount, secondaryCount, globalQualityWeight, out bindposes, out FaunaSpineIkConfigDTO1610 config, out FaunaSpineBoneDTO1610[] spineRows);
                vertices = new NativeArray<float3>(vertexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                for (int i = 0; i < vertexCount; i++)
                    vertices[i] = vertexList[i];

                segments = BuildSegmentMatrices(bones, root.transform);
                bonesPerVertex = new NativeArray<byte>(vertexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                weights = new NativeArray<BoneWeight1>(vertexCount * 4, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                audits = new NativeArray<FaunaVertexWeightAuditDTO1610>(vertexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                float influenceRadiusMeters = ResolveSkinningInfluenceRadius(sourceMesh.bounds, axis);
                for (int i = 0; i < vertexCount; i++)
                    bonesPerVertex[i] = 4;

                Stopwatch skinningStopwatch = Stopwatch.StartNew();
                JobHandle handle = new CalculateVertexWeightsJob1610
                {
                    Vertices = vertices,
                    BoneSegments = segments,
                    Weights = weights,
                    Audits = audits,
                    MaxInfluenceDistanceMeters = influenceRadiusMeters,
                    FalloffPower = math.lerp(1.4f, 2.8f, math.saturate(globalQualityWeight))
                }.Schedule(vertexCount, 128);
                handle.Complete();
                skinningStopwatch.Stop();

                int failures = CountWeightFailures(audits, 0.0001f, out int isolated);
                if (isolated > 0)
                {
                    Debug.LogError("[FaunaRigger1610] Vertex isolation after bounds-aware influence radius. isolated=" +
                                   isolated.ToString(CultureInfo.InvariantCulture) +
                                   " radiusMeters=" + influenceRadiusMeters.ToString("F3", CultureInfo.InvariantCulture) +
                                   ". Generation rejected; use a cleaner mesh or a VAT preset.");
                    return false;
                }

                if (failures > 0)
                {
                    Debug.LogError("[FaunaRigger1610] Weight normalization failure. count=" + failures);
                    return false;
                }

                Mesh riggedMesh = Object.Instantiate(sourceMesh);
                riggedMesh.name = "GEN_FaunaRig1610_" + presetToken + "_" + safeToken + "_Mesh";
                riggedMesh.bindposes = bindposes;
                riggedMesh.SetBoneWeights(bonesPerVertex, weights);
                BakeWrinkleMask(riggedMesh, axis, spineCount);
                riggedMesh.RecalculateBounds();

                string metadataPath = string.Empty;
                if (requiresRuntimeSpineMetadata)
                {
                    if (spineCount < MinRuntimeSpineIkSegments)
                    {
                        Debug.LogError("[FaunaRigger1610] Runtime Spine-IK metadata requires at least " +
                                       MinRuntimeSpineIkSegments.ToString(CultureInfo.InvariantCulture) +
                                       " spine rows. preset=" + preset);
                        return false;
                    }

                    if (!TryPrepareExistingFaunaKinematics(root, config, out Component kinematicsComponent))
                    {
                        Debug.LogError("[FaunaRigger1610] FaunaKinematicsRuntime metadata injection failed. Generation rejected.");
                        return false;
                    }

                    metadataPath = WriteSpineMetadata(config, spineRows, preset, safeToken);
                    if (string.IsNullOrEmpty(metadataPath))
                    {
                        Debug.LogError("[FaunaRigger1610] Spine metadata asset write failed. Generation rejected.");
                        return false;
                    }

                    if (!TryBindGeneratedRigDefinitionBinary(kinematicsComponent, metadataPath))
                    {
                        Debug.LogError("[FaunaRigger1610] Generated H8LR TextAsset binding failed. Generation rejected.");
                        return false;
                    }
                }

                string meshPath = MeshOutputRoot + "/" + riggedMesh.name + ".asset";
                Mesh meshAsset = CreateOrUpdateMeshAsset(meshPath, riggedMesh);
                SkinnedMeshRenderer renderer = root.AddComponent<SkinnedMeshRenderer>();
                renderer.sharedMesh = meshAsset;
                renderer.bones = bones;
                renderer.rootBone = bones.Length > 0 ? bones[0] : root.transform;
                if (sourceMaterial != null)
                    renderer.sharedMaterial = sourceMaterial;

                string prefabPath = PrefabOutputRoot + "/" + "GEN_FaunaRig1610_" + presetToken + "_" + safeToken + ".prefab";
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                if (prefab == null)
                {
                    Debug.LogError("[FaunaRigger1610] Prefab save failed. path=" + prefabPath);
                    return false;
                }

                EditorUtility.SetDirty(meshAsset);
                AssetDatabase.SaveAssets();

                FaunaRigMetrics1610 metrics = default;
                metrics.SourceVertexCount = vertexCount;
                metrics.SourceTriangleCount = triangleCount;
                metrics.BoneCount = bones.Length;
                metrics.SpineBoneCount = spineCount;
                metrics.SecondaryBoneCount = secondaryCount;
                metrics.IsolatedVertexCount = isolated;
                metrics.WeightNormalizationFailures = failures;
                metrics.SkinningMilliseconds = skinningStopwatch.Elapsed.TotalMilliseconds;
                metrics.TotalMilliseconds = totalStopwatch.Elapsed.TotalMilliseconds;
                metrics.MeshHash = ComputeMeshHash(meshAsset);
                metrics.WeightHash = ComputeWeightHash(weights);
                output = new FaunaRigOutput1610(meshPath, string.Empty, prefabPath, metadataPath, metrics);
                LogFinalMetricSummary(in output);
                return true;
            }
            finally
            {
                if (audits.IsCreated) audits.Dispose();
                if (weights.IsCreated) weights.Dispose();
                if (bonesPerVertex.IsCreated) bonesPerVertex.Dispose();
                if (segments.IsCreated) segments.Dispose();
                if (vertices.IsCreated) vertices.Dispose();
                Object.DestroyImmediate(root);
            }
        }

        private static bool TryBakeVatSwarmPrefab(
            Mesh sourceMesh,
            Material sourceMaterial,
            string safeToken,
            int frameCount,
            float globalQualityWeight,
            Stopwatch totalStopwatch,
            out FaunaRigOutput1610 output)
        {
            output = default;
            List<Vector3> vertexList = new List<Vector3>(sourceMesh.vertexCount); // COLD ALLOC: List<Vector3>[source vertex count] - editor VAT extraction scratch - owner: FaunaOfflineRigger1610
            sourceMesh.GetVertices(vertexList);
            int vertexCount = vertexList.Count;
            int safeFrameCount = math.max(1, frameCount);
            int maxTextureWidth = math.max(1, SystemInfo.maxTextureSize);
            if (!SystemInfo.SupportsTextureFormat(TextureFormat.RGBAFloat))
            {
                Debug.LogError("[FaunaRigger1610] VAT bake rejected. TextureFormat.RGBAFloat is unsupported by this graphics backend.");
                return false;
            }

            if (vertexCount > maxTextureWidth)
            {
                Debug.LogError("[FaunaRigger1610] VAT bake rejected. vertexCount=" +
                               vertexCount.ToString(CultureInfo.InvariantCulture) +
                               " exceeds SystemInfo.maxTextureSize=" +
                               maxTextureWidth.ToString(CultureInfo.InvariantCulture) +
                               ". Use a lower-poly swarm mesh or skeletal rig preset.");
                return false;
            }

            if (safeFrameCount > maxTextureWidth)
            {
                Debug.LogError("[FaunaRigger1610] VAT bake rejected. frameCount=" +
                               safeFrameCount.ToString(CultureInfo.InvariantCulture) +
                               " exceeds SystemInfo.maxTextureSize=" +
                               maxTextureWidth.ToString(CultureInfo.InvariantCulture) +
                               ". Reduce VAT frames or split animation variants.");
                return false;
            }

            long vatPixelCount = (long)vertexCount * safeFrameCount;
            long vatBytes = vatPixelCount * VatBytesPerPixel;
            if (vatPixelCount > int.MaxValue || vatBytes > MaxCompactVatBytes)
            {
                Debug.LogError("[FaunaRigger1610] VAT bake rejected. pixels=" +
                               vatPixelCount.ToString(CultureInfo.InvariantCulture) +
                               " bytes=" + vatBytes.ToString(CultureInfo.InvariantCulture) +
                               " budgetBytes=" + MaxCompactVatBytes.ToString(CultureInfo.InvariantCulture) +
                               ". Reduce swarm vertex count, frame count, or split the school into LOD lanes.");
                return false;
            }

            FaunaMeshAxisDTO1610 axis = AnalyzeAxis(sourceMesh.bounds);
            float3 side = StablePerpendicular(axis.Axis);

            NativeArray<float3> vertices = default;
            NativeArray<float4> pixels = default;
            Texture2D vatTexture = null;
            GameObject root = null;
            try
            {
                vertices = new NativeArray<float3>(vertexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                for (int i = 0; i < vertexCount; i++)
                    vertices[i] = vertexList[i];

                pixels = new NativeArray<float4>(vertexCount * safeFrameCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                Stopwatch vatStopwatch = Stopwatch.StartNew();
                JobHandle handle = new BakeSwarmVatJob1610
                {
                    Vertices = vertices,
                    OutputPixels = pixels,
                    VertexCount = vertexCount,
                    FrameCount = safeFrameCount,
                    AxisStart = axis.Center - axis.Axis * axis.Length * 0.5f,
                    AxisDirection = axis.Axis,
                    SideDirection = side,
                    LengthMeters = axis.Length,
                    AmplitudeMeters = math.max(0.01f, axis.Length * 0.035f),
                    WaveCycles = math.lerp(1.25f, 2.75f, math.saturate(globalQualityWeight)),
                    GlobalQualityWeight = globalQualityWeight
                }.Schedule(pixels.Length, 128);
                handle.Complete();
                vatStopwatch.Stop();

                vatTexture = new Texture2D(vertexCount, safeFrameCount, TextureFormat.RGBAFloat, false, true);
                vatTexture.name = "GEN_FaunaVAT1610_" + safeToken + "_Position";
                vatTexture.SetPixelData(pixels, 0);
                vatTexture.Apply(false, false);
                string vatPath = VatOutputRoot + "/" + vatTexture.name + ".asset";
                Texture2D vatAsset = CreateOrUpdateTextureAsset(vatPath, vatTexture);
                vatTexture = null;

                Mesh meshAsset = CreateOrUpdateMeshAsset(MeshOutputRoot + "/" + "GEN_FaunaVAT1610_" + safeToken + "_Mesh.asset", Object.Instantiate(sourceMesh));
                root = new GameObject("GEN_FaunaVAT1610_" + safeToken);
                MeshFilter filter = root.AddComponent<MeshFilter>();
                filter.sharedMesh = meshAsset;
                MeshRenderer renderer = root.AddComponent<MeshRenderer>();
                Material material = CreateVatMaterial(sourceMaterial, safeToken, vatAsset, vertexCount, safeFrameCount, globalQualityWeight);
                if (material != null)
                    renderer.sharedMaterial = material;

                WriteVatMetadata(vertexCount, safeFrameCount, axis, globalQualityWeight, vatStopwatch.Elapsed.TotalMilliseconds);
                string prefabPath = PrefabOutputRoot + "/" + "GEN_FaunaVAT1610_" + safeToken + ".prefab";
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                if (prefab == null)
                {
                    Debug.LogError("[FaunaRigger1610] VAT prefab save failed. path=" + prefabPath);
                    return false;
                }

                AssetDatabase.SaveAssets();

                FaunaRigMetrics1610 metrics = default;
                metrics.SourceVertexCount = vertexCount;
                metrics.SourceTriangleCount = CountMeshTriangles(sourceMesh);
                metrics.VatVertexCount = vertexCount;
                metrics.VatFrameCount = safeFrameCount;
                metrics.VatMilliseconds = vatStopwatch.Elapsed.TotalMilliseconds;
                metrics.TotalMilliseconds = totalStopwatch.Elapsed.TotalMilliseconds;
                metrics.MaxVatPrecisionError = 0f;
                metrics.MeshHash = ComputeMeshHash(meshAsset);
                metrics.VatHash = ComputeVatHash(pixels);
                output = new FaunaRigOutput1610(meshAsset != null ? AssetDatabase.GetAssetPath(meshAsset) : string.Empty, vatPath, prefabPath, string.Empty, metrics);
                LogFinalMetricSummary(in output);
                return true;
            }
            finally
            {
                if (pixels.IsCreated) pixels.Dispose();
                if (vertices.IsCreated) vertices.Dispose();
                if (root != null) Object.DestroyImmediate(root);
                if (vatTexture != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(vatTexture)))
                    Object.DestroyImmediate(vatTexture);
            }
        }

        private static Transform[] CreateBoneHierarchy(
            Transform root,
            FaunaMeshAxisDTO1610 axis,
            Bounds bounds,
            int spineCount,
            int secondaryCount,
            float globalQualityWeight,
            out Matrix4x4[] bindposes,
            out FaunaSpineIkConfigDTO1610 config,
            out FaunaSpineBoneDTO1610[] spineRows)
        {
            int safeSpineCount = math.max(1, spineCount);
            int totalCount = math.max(1, safeSpineCount + math.max(0, secondaryCount));
            Transform[] bones = new Transform[totalCount];
            bindposes = new Matrix4x4[totalCount];
            spineRows = new FaunaSpineBoneDTO1610[totalCount];
            float length = math.max(0.001f, axis.Length);
            float segmentLength = safeSpineCount > 1 ? length / (safeSpineCount - 1) : length;
            float3 start = axis.Center - axis.Axis * length * 0.5f;
            for (int i = 0; i < safeSpineCount; i++)
            {
                GameObject bone = new GameObject("Spine_" + i.ToString("00", CultureInfo.InvariantCulture));
                Transform parent = i == 0 ? root : bones[i - 1];
                bone.transform.SetParent(parent, false);
                float3 worldPos = start + axis.Axis * (segmentLength * i);
                bone.transform.localPosition = i == 0 ? (Vector3)worldPos : (Vector3)(axis.Axis * segmentLength);
                bone.transform.localRotation = Quaternion.identity;
                bone.transform.localScale = Vector3.one;
                bones[i] = bone.transform;
                spineRows[i] = new FaunaSpineBoneDTO1610
                {
                    LocalOffset = i == 0 ? worldPos : axis.Axis * segmentLength,
                    SegmentLengthMeters = segmentLength,
                    MaxBendRadians = math.radians(38f),
                    ParentIndex = i == 0 ? -1 : i - 1,
                    Flags = 1u
                };
            }

            float3 side = StablePerpendicular(axis.Axis);
            float lateralSpan = math.max(bounds.extents.x, math.max(bounds.extents.y, bounds.extents.z));
            for (int i = 0; i < secondaryCount; i++)
            {
                int boneIndex = safeSpineCount + i;
                int anchorIndex = math.clamp((int)math.round(math.lerp(1f, safeSpineCount - 2f, (i + 1f) / (secondaryCount + 1f))), 0, safeSpineCount - 1);
                GameObject bone = new GameObject("Fin_" + i.ToString("00", CultureInfo.InvariantCulture));
                bone.transform.SetParent(bones[anchorIndex], false);
                float sign = (i & 1) == 0 ? 1f : -1f;
                float3 offset = side * (sign * math.max(0.05f, lateralSpan * 0.62f));
                bone.transform.localPosition = (Vector3)offset;
                bone.transform.localRotation = Quaternion.identity;
                bone.transform.localScale = Vector3.one;
                bones[boneIndex] = bone.transform;
                spineRows[boneIndex] = new FaunaSpineBoneDTO1610
                {
                    LocalOffset = offset,
                    SegmentLengthMeters = math.length(offset),
                    MaxBendRadians = math.radians(22f),
                    ParentIndex = anchorIndex,
                    Flags = 2u
                };
            }

            for (int i = 0; i < totalCount; i++)
                bindposes[i] = bones[i].worldToLocalMatrix * root.localToWorldMatrix;

            config = new FaunaSpineIkConfigDTO1610
            {
                CreatureHash = HashFnv1a(root.name),
                Flags = 1u,
                BoneStart = 0,
                BoneCount = totalCount,
                SpineBoneCount = safeSpineCount,
                SecondaryBoneCount = secondaryCount,
                SegmentLengthMeters = segmentLength,
                MaxBendRadians = math.radians(38f),
                BodyRadiusMeters = lateralSpan,
                GlobalQualityWeight = math.saturate(math.select(1f, globalQualityWeight, math.isfinite(globalQualityWeight))),
                AxisLocal = axis.Axis,
                MetadataVersion = SpineMetadataVersion
            };
            return bones;
        }

        private static NativeArray<float4x4> BuildSegmentMatrices(Transform[] bones, Transform root)
        {
            NativeArray<float4x4> segments = new NativeArray<float4x4>(math.max(1, bones.Length), Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            for (int i = 0; i < bones.Length; i++)
            {
                Vector3 start = root.InverseTransformPoint(bones[i].position);
                Vector3 end;
                if (i + 1 < bones.Length && bones[i + 1].parent == bones[i])
                    end = root.InverseTransformPoint(bones[i + 1].position);
                else if (bones[i].parent != null && bones[i].parent != root)
                    end = start + (start - root.InverseTransformPoint(bones[i].parent.position));
                else
                    end = start + Vector3.forward * 0.1f;
                segments[i] = EncodeBoneSegment(start, end, i);
            }

            return segments;
        }

        private static float4x4 EncodeBoneSegment(float3 start, float3 end, int boneIndex)
        {
            return new float4x4(
                new float4(start, 0f),
                new float4(end, 0f),
                new float4(boneIndex, 0f, 0f, 0f),
                new float4(0f, 0f, 0f, 1f));
        }

        private static void BakeWrinkleMask(Mesh mesh, FaunaMeshAxisDTO1610 axis, int spineCount)
        {
            List<Vector3> vertices = new List<Vector3>(mesh.vertexCount); // COLD ALLOC: List<Vector3>[mesh vertex count] - editor wrinkle mask scratch - owner: FaunaOfflineRigger1610
            List<Color32> colors = new List<Color32>(mesh.vertexCount); // COLD ALLOC: List<Color32>[mesh vertex count] - editor wrinkle mask output - owner: FaunaOfflineRigger1610
            mesh.GetVertices(vertices);
            float length = math.max(0.0001f, axis.Length);
            float3 start = axis.Center - axis.Axis * length * 0.5f;
            int jointCount = math.max(2, spineCount);
            for (int i = 0; i < vertices.Count; i++)
            {
                float t = math.saturate(math.dot((float3)vertices[i] - start, axis.Axis) * math.rcp(length));
                float joint = math.round(t * (jointCount - 1)) * math.rcp(jointCount - 1);
                float tension = 1f - math.saturate(math.abs(t - joint) * jointCount * 2f);
                byte green = (byte)math.clamp((int)math.round(tension * 255f), 0, 255);
                colors.Add(new Color32(32, green, 24, 255));
            }

            mesh.SetColors(colors);
        }

        private static int CountWeightFailures(NativeArray<FaunaVertexWeightAuditDTO1610> audits, float tolerance, out int isolated)
        {
            isolated = 0;
            int failures = 0;
            if (!audits.IsCreated)
                return 1;

            for (int i = 0; i < audits.Length; i++)
            {
                FaunaVertexWeightAuditDTO1610 audit = audits[i];
                if ((audit.Flags & 1u) != 0u)
                    isolated++;
                if (!math.isfinite(audit.WeightSum) || math.abs(audit.WeightSum - 1f) > tolerance)
                    failures++;
            }

            return failures;
        }

        private static string WriteSpineMetadata(FaunaSpineIkConfigDTO1610 config, FaunaSpineBoneDTO1610[] rows, FaunaRigPreset1610 preset, string token)
        {
            if (rows == null || rows.Length < 2 || config.SpineBoneCount < 2)
                return string.Empty;

            int rowCount = math.clamp(math.min(rows.Length, config.SpineBoneCount), 2, MaxRuntimeLeviathanIkSegments);
            byte[] payload = new byte[GeneratedRigHeaderBytes + rowCount * GeneratedRigRowBytes];
            WriteUInt32Little(payload, 0, GeneratedRigMagicH8lr);
            WriteUInt32Little(payload, 4, SpineMetadataVersion);
            WriteUInt32Little(payload, 8, (uint)rowCount);
            WriteUInt32Little(payload, 12, GeneratedRigHeaderBytes);
            for (int i = 0; i < rowCount; i++)
            {
                FaunaSpineBoneDTO1610 row = rows[i];
                int rowOffset = GeneratedRigHeaderBytes + i * GeneratedRigRowBytes;
                WriteInt32Little(payload, rowOffset, i == 0 ? -1 : math.clamp(row.ParentIndex, 0, i - 1));
                WriteUInt16Little(payload, rowOffset + 4, 0);
                WriteUInt16Little(payload, rowOffset + 6, 1);
                WriteFloat32Little(payload, rowOffset + 8, math.max(0.001f, row.SegmentLengthMeters));
                WriteFloat32Little(payload, rowOffset + 12, math.max(0f, row.MaxBendRadians));
            }

            EnsureFolder(SpineMetadataOutputRoot);
            string assetPath = SpineMetadataOutputRoot + "/" + "GEN_FaunaRig1610_" + preset + "_" + token + "_SpineH8LR.bytes";
            string absolutePath = Path.Combine(Directory.GetCurrentDirectory(), assetPath.Replace('/', Path.DirectorySeparatorChar));
            try
            {
                WriteProductAssetBytesSafely(absolutePath, payload);
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is InvalidOperationException || ex is NotSupportedException)
            {
                Debug.LogError("[FaunaRigger1610] Spine metadata product asset write/import failed. path=" + assetPath +
                               " error=" + ex.GetType().Name + ": " + ex.Message);
                return string.Empty;
            }

            Debug.Log("[FaunaRigger1610] Spine metadata baked into prefab route. token=" + token +
                      " preset=" + preset +
                      " bones=" + config.BoneCount.ToString(CultureInfo.InvariantCulture) +
                      " spine=" + config.SpineBoneCount.ToString(CultureInfo.InvariantCulture) +
                      " secondary=" + config.SecondaryBoneCount.ToString(CultureInfo.InvariantCulture) +
                      " rows=" + rowCount.ToString(CultureInfo.InvariantCulture) +
                      " runtimeCap=" + MaxRuntimeLeviathanIkSegments.ToString(CultureInfo.InvariantCulture) +
                      " bytes=" + payload.Length.ToString(CultureInfo.InvariantCulture) +
                      " quality=" + config.GlobalQualityWeight.ToString("F3", CultureInfo.InvariantCulture));
            return assetPath;
        }

        private static void WriteVatMetadata(int vertexCount, int frameCount, FaunaMeshAxisDTO1610 axis, float quality, double milliseconds)
        {
            Debug.Log("[FaunaRigger1610] VAT metadata. format=RGBAFloat width=" +
                      vertexCount.ToString(CultureInfo.InvariantCulture) +
                      " height=" + frameCount.ToString(CultureInfo.InvariantCulture) +
                      " quality=" + math.saturate(quality).ToString("F3", CultureInfo.InvariantCulture) +
                      " axisLength=" + axis.Length.ToString("F3", CultureInfo.InvariantCulture) +
                      " bakeMs=" + milliseconds.ToString("F3", CultureInfo.InvariantCulture));
        }

        private static bool TryPrepareExistingFaunaKinematics(GameObject root, FaunaSpineIkConfigDTO1610 config, out Component component)
        {
            component = null;
            Type type = ResolveTypeByFullName("Hecton8.AI.FaunaKinematicsRuntime");
            if (type == null || root == null)
                return false;

            component = root.GetComponent(type);
            if (component == null)
                component = root.AddComponent(type);

            if (component == null)
                return false;

            SerializedObject serialized = new SerializedObject(component);
            if (!HasSerializedObjectReference(serialized, "_generatedRigDefinitionBinary"))
            {
                Debug.LogError("[FaunaRigger1610] FaunaKinematicsRuntime does not expose _generatedRigDefinitionBinary. Metadata route rejected.");
                component = null;
                return false;
            }

            TrySetSerializedInt(serialized, "_maximumQualitySegmentCount", math.clamp(config.SpineBoneCount, 8, 20));
            TrySetSerializedFloat(serialized, "_segmentLength", math.max(0.25f, config.SegmentLengthMeters));
            TrySetSerializedFloat(serialized, "_bodyRadius", math.max(0.05f, config.BodyRadiusMeters));
            TrySetSerializedInt(serialized, "_maximumQualityConstraintIterations", 8);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private static bool TryBindGeneratedRigDefinitionBinary(Component component, string metadataPath)
        {
            if (component == null || string.IsNullOrEmpty(metadataPath))
                return false;

            TextAsset metadataAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(metadataPath);
            if (metadataAsset == null)
            {
                Debug.LogError("[FaunaRigger1610] Generated H8LR TextAsset did not import. path=" + metadataPath);
                return false;
            }

            SerializedObject serialized = new SerializedObject(component);
            if (!TrySetSerializedObjectReference(serialized, "_generatedRigDefinitionBinary", metadataAsset))
            {
                Debug.LogError("[FaunaRigger1610] FaunaKinematicsRuntime does not expose _generatedRigDefinitionBinary. Metadata route rejected.");
                return false;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private static bool HasSerializedObjectReference(SerializedObject serialized, string propertyName)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            return property != null && property.propertyType == SerializedPropertyType.ObjectReference;
        }

        private static Type ResolveTypeByFullName(string fullName)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type type = assemblies[i].GetType(fullName, false);
                if (type != null)
                    return type;
            }

            return null;
        }

        private static void TrySetSerializedInt(SerializedObject serialized, string propertyName, int value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null && property.propertyType == SerializedPropertyType.Integer)
                property.intValue = value;
        }

        private static void TrySetSerializedFloat(SerializedObject serialized, string propertyName, float value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null && property.propertyType == SerializedPropertyType.Float)
                property.floatValue = value;
        }

        private static bool TrySetSerializedObjectReference(SerializedObject serialized, string propertyName, Object value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null && property.propertyType == SerializedPropertyType.ObjectReference)
            {
                property.objectReferenceValue = value;
                return true;
            }

            return false;
        }

        private static void WriteInt32Little(byte[] bytes, int offset, int value)
        {
            WriteUInt32Little(bytes, offset, unchecked((uint)value));
        }

        private static void WriteUInt32Little(byte[] bytes, int offset, uint value)
        {
            bytes[offset] = (byte)value;
            bytes[offset + 1] = (byte)(value >> 8);
            bytes[offset + 2] = (byte)(value >> 16);
            bytes[offset + 3] = (byte)(value >> 24);
        }

        private static void WriteUInt16Little(byte[] bytes, int offset, ushort value)
        {
            bytes[offset] = (byte)value;
            bytes[offset + 1] = (byte)(value >> 8);
        }

        private static void WriteFloat32Little(byte[] bytes, int offset, float value)
        {
            WriteUInt32Little(bytes, offset, unchecked((uint)BitConverter.SingleToInt32Bits(value)));
        }

        private static void WriteProductAssetBytesSafely(string absolutePath, byte[] payload)
        {
            if (string.IsNullOrEmpty(absolutePath) || payload == null || payload.Length == 0)
                throw new InvalidOperationException("Invalid fauna product asset write request.");

            string tempPath = absolutePath + ".tmp";
            if (File.Exists(tempPath))
                File.Delete(tempPath);

            bool committed = false;
            try
            {
                File.WriteAllBytes(tempPath, payload);
                if (File.Exists(absolutePath))
                    File.Replace(tempPath, absolutePath, null);
                else
                    File.Move(tempPath, absolutePath);

                committed = true;
            }
            finally
            {
                if (!committed && File.Exists(tempPath))
                {
                    try
                    {
                        File.Delete(tempPath);
                    }
                    catch (IOException)
                    {
                    }
                    catch (UnauthorizedAccessException)
                    {
                    }
                }
            }
        }

        private static FaunaMeshAxisDTO1610 AnalyzeAxis(Bounds bounds)
        {
            Vector3 size = bounds.size;
            uint axisIndex = 2u;
            float3 axis = new float3(0f, 0f, 1f);
            float length = math.max(0.001f, size.z);
            if (size.x >= size.y && size.x >= size.z)
            {
                axisIndex = 0u;
                axis = new float3(1f, 0f, 0f);
                length = math.max(0.001f, size.x);
            }
            else if (size.y >= size.x && size.y >= size.z)
            {
                axisIndex = 1u;
                axis = new float3(0f, 1f, 0f);
                length = math.max(0.001f, size.y);
            }

            return new FaunaMeshAxisDTO1610
            {
                Center = bounds.center,
                Length = length,
                Axis = axis,
                AxisIndex = axisIndex
            };
        }

        private static float3 StablePerpendicular(float3 axis)
        {
            float3 up = math.abs(axis.y) < 0.82f ? new float3(0f, 1f, 0f) : new float3(1f, 0f, 0f);
            return math.normalizesafe(math.cross(up, axis), new float3(1f, 0f, 0f));
        }

        internal static int ResolveBoneLimit(FaunaRigPreset1610 preset)
        {
            switch (preset)
            {
                case FaunaRigPreset1610.SmallFish:
                    return MaxSmallFishBones;
                case FaunaRigPreset1610.MediumPredator:
                    return MaxMediumPredatorBones;
                case FaunaRigPreset1610.Leviathan:
                    return MaxLeviathanBones;
                default:
                    return 0;
            }
        }

        internal static int ResolveMinimumSkinnedBoneCount(FaunaRigPreset1610 preset)
        {
            if (preset == FaunaRigPreset1610.MediumPredator)
                return math.min(MinRuntimeSpineIkSegments + 4, MaxMediumPredatorBones);
            if (preset == FaunaRigPreset1610.Leviathan)
                return math.min(MinRuntimeSpineIkSegments, MaxLeviathanBones);
            return 2;
        }

        internal static bool RequiresRuntimeSpineMetadata(FaunaRigPreset1610 preset)
        {
            return preset == FaunaRigPreset1610.MediumPredator ||
                   preset == FaunaRigPreset1610.Leviathan;
        }

        private static int ResolveSpineBoneCount(FaunaRigPreset1610 preset, int boneCount)
        {
            if (preset == FaunaRigPreset1610.MediumPredator)
                return math.max(2, boneCount - 4);
            if (preset == FaunaRigPreset1610.Leviathan)
                return math.max(8, boneCount - 6);
            return math.max(1, boneCount);
        }

        private static float ResolveSkinningInfluenceRadius(Bounds bounds, FaunaMeshAxisDTO1610 axis)
        {
            Vector3 extents = bounds.extents;
            float diagonal = math.length(new float3(extents.x, extents.y, extents.z));
            float radius = math.max(math.max(0.5f, axis.Length), diagonal * 2f);
            return math.isfinite(radius) ? radius : 0.5f;
        }

        private static int ResolveBoneLimitFromName(string path)
        {
            string lower = path.ToLowerInvariant();
            if (lower.Contains("leviathan"))
                return MaxLeviathanBones;
            if (lower.Contains("medium") || lower.Contains("predator"))
                return MaxMediumPredatorBones;
            return MaxSmallFishBones;
        }

        private static bool TryAnalyzeMeshAsset(string assetPath, out string meshSummary)
        {
            meshSummary = null;
            Object[] objects = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            int vertexCount = 0;
            int triangleCount = 0;
            Bounds bounds = default;
            bool hasBounds = false;
            for (int i = 0; i < objects.Length; i++)
            {
                Mesh mesh = objects[i] as Mesh;
                if (mesh == null)
                    continue;

                vertexCount += mesh.vertexCount;
                triangleCount += CountMeshTriangles(mesh);
                if (!hasBounds)
                {
                    bounds = mesh.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(mesh.bounds);
                }
            }

            if (vertexCount <= 0)
                return false;

            FaunaLocomotionStyle1610 style = InferLocomotion(assetPath);
            StringBuilder builder = new StringBuilder(1024);
            builder.Append("[FaunaRigger1610] mesh=");
            builder.Append(assetPath);
            builder.Append(" vertices=");
            builder.Append(vertexCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(" triangles=");
            builder.Append(triangleCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(" bounds=");
            builder.Append(bounds.size.x.ToString("F3", CultureInfo.InvariantCulture));
            builder.Append('x');
            builder.Append(bounds.size.y.ToString("F3", CultureInfo.InvariantCulture));
            builder.Append('x');
            builder.Append(bounds.size.z.ToString("F3", CultureInfo.InvariantCulture));
            builder.Append(" locomotion=");
            builder.Append(style.ToString());
            meshSummary = builder.ToString();
            return true;
        }

        private static FaunaLocomotionStyle1610 InferLocomotion(string path)
        {
            string lower = path.ToLowerInvariant();
            if (lower.Contains("swarm") || lower.Contains("school") || lower.Contains("minnow"))
                return FaunaLocomotionStyle1610.VatSwarm;
            if (lower.Contains("crab") || lower.Contains("crawler") || lower.Contains("tentacle"))
                return FaunaLocomotionStyle1610.TentacleCrawler;
            if (lower.Contains("ray") || lower.Contains("paddle") || lower.Contains("fin"))
                return FaunaLocomotionStyle1610.PectoralPaddler;
            if (lower.Contains("eel") || lower.Contains("leviathan") || lower.Contains("serpent"))
                return FaunaLocomotionStyle1610.SerpentineSwimmer;
            return FaunaLocomotionStyle1610.Unknown;
        }

        private static Mesh CreateOrUpdateMeshAsset(string assetPath, Mesh mesh)
        {
            EnsureFolder(Path.GetDirectoryName(assetPath).Replace('\\', '/'));
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (existing != null)
            {
                EditorUtility.CopySerialized(mesh, existing);
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(mesh);
                return existing;
            }

            AssetDatabase.CreateAsset(mesh, assetPath);
            return mesh;
        }

        private static Texture2D CreateOrUpdateTextureAsset(string assetPath, Texture2D texture)
        {
            EnsureFolder(Path.GetDirectoryName(assetPath).Replace('\\', '/'));
            Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (existing != null)
            {
                EditorUtility.CopySerialized(texture, existing);
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(texture);
                return existing;
            }

            AssetDatabase.CreateAsset(texture, assetPath);
            return texture;
        }

        private static Material CreateVatMaterial(Material sourceMaterial, string safeToken, Texture2D vatTexture, int vertexCount, int frameCount, float globalQualityWeight)
        {
            if (sourceMaterial == null)
                return null;

            Material material = new Material(sourceMaterial);
            material.name = "MAT_FaunaVAT1610_" + safeToken;
            float quality = math.saturate(math.select(1f, globalQualityWeight, math.isfinite(globalQualityWeight)));
            if (material.HasProperty("_VatEnabled"))
                material.SetFloat("_VatEnabled", 1f);
            if (material.HasProperty("_VATPositionTex"))
                material.SetTexture("_VATPositionTex", vatTexture);
            if (material.HasProperty("_H8VatPositionTex"))
                material.SetTexture("_H8VatPositionTex", vatTexture);
            if (material.HasProperty("_VatFrameCount"))
                material.SetFloat("_VatFrameCount", math.max(1, frameCount));
            if (material.HasProperty("_VatVertexCount"))
                material.SetFloat("_VatVertexCount", math.max(1, vertexCount));
            if (material.HasProperty("_VatPlaybackSpeed"))
                material.SetFloat("_VatPlaybackSpeed", math.lerp(0.75f, 1.35f, quality));
            if (material.HasProperty("_VatNormalBlend"))
                material.SetFloat("_VatNormalBlend", math.lerp(0.35f, 1f, quality));
            if (material.HasProperty("_VatPositionScale"))
                material.SetFloat("_VatPositionScale", 1f);
            string materialFolder = "Assets/_Project/Art/Materials/Fauna/VAT1610";
            EnsureFolder(materialFolder);
            string path = materialFolder + "/" + material.name + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(material, existing);
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(material);
                return existing;
            }

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static int CountMeshTriangles(Mesh mesh)
        {
            if (mesh == null)
                return 0;

            long indices = 0L;
            for (int i = 0; i < mesh.subMeshCount; i++)
                indices += mesh.GetIndexCount(i);
            return (int)Math.Min(int.MaxValue, indices / 3L);
        }

        private static ulong ComputeMeshHash(Mesh mesh)
        {
            if (mesh == null)
                return 0ul;

            List<Vector3> vertices = new List<Vector3>(mesh.vertexCount); // COLD ALLOC: List<Vector3>[mesh vertex count] - editor hash scratch - owner: FaunaOfflineRigger1610
            mesh.GetVertices(vertices);
            ulong hash = 1469598103934665603ul;
            for (int i = 0; i < vertices.Count; i++)
            {
                Vector3 v = vertices[i];
                hash = MixHash(hash, (uint)BitConverter.SingleToInt32Bits(v.x));
                hash = MixHash(hash, (uint)BitConverter.SingleToInt32Bits(v.y));
                hash = MixHash(hash, (uint)BitConverter.SingleToInt32Bits(v.z));
            }

            return hash;
        }

        private static ulong ComputeWeightHash(NativeArray<BoneWeight1> weights)
        {
            if (!weights.IsCreated)
                return 0ul;

            ulong hash = 1469598103934665603ul;
            for (int i = 0; i < weights.Length; i++)
            {
                BoneWeight1 weight = weights[i];
                hash = MixHash(hash, (uint)weight.boneIndex);
                hash = MixHash(hash, (uint)BitConverter.SingleToInt32Bits(weight.weight));
            }

            return hash;
        }

        private static ulong ComputeVatHash(NativeArray<float4> pixels)
        {
            if (!pixels.IsCreated)
                return 0ul;

            ulong hash = 1469598103934665603ul;
            for (int i = 0; i < pixels.Length; i++)
            {
                float4 p = pixels[i];
                hash = MixHash(hash, (uint)BitConverter.SingleToInt32Bits(p.x));
                hash = MixHash(hash, (uint)BitConverter.SingleToInt32Bits(p.y));
                hash = MixHash(hash, (uint)BitConverter.SingleToInt32Bits(p.z));
                hash = MixHash(hash, (uint)BitConverter.SingleToInt32Bits(p.w));
            }

            return hash;
        }

        private static ulong MixHash(ulong hash, uint value)
        {
            hash ^= value;
            hash *= 1099511628211ul;
            return hash;
        }

        private static string ComputeSha256ForGeneratorFiles()
        {
            string folder = Path.Combine(Directory.GetCurrentDirectory(), "Assets/_Project/Editor/Generators/Fauna");
            if (!Directory.Exists(folder))
                return string.Empty;

            string[] files = Directory.GetFiles(folder, "*.cs", SearchOption.TopDirectoryOnly);
            Array.Sort(files, StringComparer.Ordinal);
            using (SHA256 sha = SHA256.Create())
            {
                for (int i = 0; i < files.Length; i++)
                {
                    byte[] bytes = File.ReadAllBytes(files[i]);
                    sha.TransformBlock(bytes, 0, bytes.Length, bytes, 0);
                }

                sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                return ToHex(sha.Hash);
            }
        }

        private static string ToHex(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                return string.Empty;

            char[] chars = new char[bytes.Length * 2];
            const string hex = "0123456789abcdef";
            for (int i = 0; i < bytes.Length; i++)
            {
                chars[i * 2] = hex[bytes[i] >> 4];
                chars[i * 2 + 1] = hex[bytes[i] & 15];
            }

            return new string(chars);
        }

        private static bool IsRawMeshExtension(string extension)
        {
            return string.Equals(extension, ".fbx", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".obj", StringComparison.OrdinalIgnoreCase);
        }

        private static string ToAssetPath(string absolutePath)
        {
            string normalized = absolutePath.Replace('\\', '/');
            int index = normalized.IndexOf("/Assets/", StringComparison.Ordinal);
            if (index < 0)
                return null;
            return normalized.Substring(index + 1);
        }

        private static string SanitizeAssetToken(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "Unnamed";

            char[] chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                if (!char.IsLetterOrDigit(c) && c != '_' && c != '-')
                    chars[i] = '_';
            }

            return new string(chars);
        }

        private static uint HashFnv1a(string value)
        {
            uint hash = 2166136261u;
            if (string.IsNullOrEmpty(value))
                return hash;

            for (int i = 0; i < value.Length; i++)
            {
                hash ^= value[i];
                hash *= 16777619u;
            }

            return hash;
        }

        private static void EnsureFolder(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath) || AssetDatabase.IsValidFolder(assetPath))
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

    internal sealed class AbyssalAnatomyStudioWindow1610 : EditorWindow
    {
        private DefaultAsset _rawFolder;
        private Mesh _mesh;
        private Material _material;
        private FaunaRigPreset1610 _preset = FaunaRigPreset1610.Leviathan;
        private int _targetBoneCount = 20;
        private int _vatFrameCount = 30;
        private float _globalQualityWeight = 0.75f;
        private string _lastStatus = "No bake run in this editor session.";

        [MenuItem("HECTON-8/Fauna/Abyssal Anatomy Studio 1610")]
        public static void Open()
        {
            AbyssalAnatomyStudioWindow1610 window = GetWindow<AbyssalAnatomyStudioWindow1610>("Abyssal Anatomy Studio");
            window.minSize = new Vector2(440f, 300f);
            window.Show();
        }

        [MenuItem("HECTON-8/Fauna/1610 Analyze Raw Meshes")]
        public static void AnalyzeRawMeshesMenu()
        {
            FaunaOfflineRigger1610.AnalyzeRawFaunaMeshes();
        }

        [MenuItem("HECTON-8/Fauna/1610 Run 1M Skinning Fuzzer")]
        public static void RunFuzzerMenu()
        {
            FaunaOfflineRigger1610.RunMockMillionVertexSkinningFuzzer();
        }

        [MenuItem("HECTON-8/Fauna/1610 Run VAT Precision Assertion")]
        public static void RunVatPrecisionMenu()
        {
            FaunaOfflineRigger1610.RunVatPrecisionAssertion();
        }

        [MenuItem("HECTON-8/Fauna/1610 Audit Generated Bone Limits")]
        public static void AuditBoneLimitsMenu()
        {
            FaunaOfflineRigger1610.RunBoneLimitComplianceAudit();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Abyssal Anatomy Studio 1610", EditorStyles.boldLabel);
            _rawFolder = (DefaultAsset)EditorGUILayout.ObjectField("Raw Mesh Folder", _rawFolder, typeof(DefaultAsset), false);
            _mesh = (Mesh)EditorGUILayout.ObjectField("Single Mesh", _mesh, typeof(Mesh), false);
            _material = (Material)EditorGUILayout.ObjectField("Material", _material, typeof(Material), false);
            _preset = (FaunaRigPreset1610)EditorGUILayout.EnumPopup("Rig Preset", _preset);
            int boneLimit = FaunaOfflineRigger1610.ResolveBoneLimit(_preset);
            if (_preset == FaunaRigPreset1610.VatSwarm)
            {
                _targetBoneCount = 0;
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.IntField("Target Bone Count", _targetBoneCount);
            }
            else
            {
                int minimumBoneCount = FaunaOfflineRigger1610.ResolveMinimumSkinnedBoneCount(_preset);
                _targetBoneCount = math.clamp(_targetBoneCount, minimumBoneCount, boneLimit);
                _targetBoneCount = EditorGUILayout.IntSlider("Target Bone Count", _targetBoneCount, minimumBoneCount, boneLimit);
            }

            using (new EditorGUI.DisabledScope(_preset != FaunaRigPreset1610.VatSwarm))
                _vatFrameCount = EditorGUILayout.IntSlider("VAT Frame Count", _vatFrameCount, 8, 120);
            _globalQualityWeight = EditorGUILayout.Slider("GlobalQualityWeight", _globalQualityWeight, 0f, 1f);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Analyze Raw Folder", GUILayout.Height(28f)))
                {
                    FaunaOfflineRigger1610.AnalyzeRawFaunaMeshes();
                    _lastStatus = "Raw mesh scan logged. Missing folder is recorded as NO_RAW_FAUNA_MESH_INPUTS.";
                }

                GUI.enabled = _mesh != null;
                if (GUILayout.Button("Rig and Bake", GUILayout.Height(28f)))
                    RigSelectedMesh();
                GUI.enabled = true;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("1M Weight Fuzzer"))
                    FaunaOfflineRigger1610.RunMockMillionVertexSkinningFuzzer();
                if (GUILayout.Button("VAT Precision"))
                    FaunaOfflineRigger1610.RunVatPrecisionAssertion();
                if (GUILayout.Button("Bone Audit"))
                    FaunaOfflineRigger1610.RunBoneLimitComplianceAudit();
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox(_lastStatus, MessageType.Info);
        }

        private void RigSelectedMesh()
        {
            if (!FaunaOfflineRigger1610.TryRigAndBakeMesh(
                    _mesh,
                    _material,
                    _preset,
                    _targetBoneCount,
                    _globalQualityWeight,
                    _vatFrameCount,
                    _mesh != null ? _mesh.name : "Selected",
                    out FaunaRigOutput1610 output))
            {
                _lastStatus = "Generation failed. Check Console for fail-closed reason.";
                return;
            }

            _lastStatus = "Generated prefab: " + output.PrefabPath + "\nMesh: " + output.MeshAssetPath + "\nVAT: " + output.VatAssetPath;
        }
    }
}
#endif
