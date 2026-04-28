using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.Core
{
    /// <summary>
    /// Shared runtime renderer for logistics pipes and relay cables.
    /// Batches all registered links into a small set of spline-built tube meshes.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class ConnectionSplineBatchRenderer : MonoBehaviour
    {
        private const int SamplesPerLink = 8;
        private const int NearPipeRadialSegments = 8;
        private const int FarPipeRadialSegments = 4;
        private const int RelayRadialSegments = 8;
        private const int DefaultBatchCapacity = 100;
        private const string PrimaryShaderName = "Universal Render Pipeline/Lit";
        private const string FallbackShaderName = "Standard";
        private const float PipeLodDistanceMeters = 40f;
        private const float PipeLodDistanceMetersSq = PipeLodDistanceMeters * PipeLodDistanceMeters;

        private const float PipeRadiusMeters = 0.06f;
        private const float RelayRadiusMeters = 0.028f;

        private const float TwoPi = math.PI * 2f;

        private enum BatchKind : byte
        {
            PipesNear = 0,
            PipesFar = 1,
            RelayPowered = 2,
            RelayUnpowered = 3
        }

        private struct LinkRegistration
        {
            public float3 Start;
            public float3 End;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct TubeVertex
        {
            public float3 Position;
            public float3 Normal;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
        private struct BuildTubeFramesJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float3> StartPoints;
            [ReadOnly] public NativeArray<float3> EndPoints;
            public NativeArray<float3> SampleCenters;
            public NativeArray<float3> SampleNormals;
            public NativeArray<float3> SampleBinormals;

            public void Execute(int linkIndex)
            {
                float3 start = StartPoints[linkIndex];
                float3 end = EndPoints[linkIndex];
                ResolveSplineControlPoints(start, end, out float3 p0, out float3 p1, out float3 p2, out float3 p3);

                int sampleBaseIndex = linkIndex * SamplesPerLink;
                float3 previousTangent = new float3(0f, 1f, 0f);
                float3 previousNormal = new float3(0f, 0f, 1f);
                float3 previousBinormal = new float3(1f, 0f, 0f);

                for (int sampleIndex = 0; sampleIndex < SamplesPerLink; sampleIndex++)
                {
                    float t = SamplesPerLink > 1 ? sampleIndex / (float)(SamplesPerLink - 1) : 0f;
                    float3 center = EvaluateCatmullRom(p0, p1, p2, p3, t);
                    float3 tangent = SafeNormalize(EvaluateCatmullRomTangent(p0, p1, p2, p3, t), new float3(0f, 1f, 0f));

                    float3 normal;
                    float3 binormal;
                    if (sampleIndex == 0)
                    {
                        ConnectionSplineBatchRenderer.ResolveInitialFrame(tangent, out normal, out binormal);
                    }
                    else
                    {
                        ConnectionSplineBatchRenderer.TransportFrame(previousTangent, tangent, previousNormal, previousBinormal, out normal, out binormal);
                    }

                    int sampleWriteIndex = sampleBaseIndex + sampleIndex;
                    SampleCenters[sampleWriteIndex] = center;
                    SampleNormals[sampleWriteIndex] = normal;
                    SampleBinormals[sampleWriteIndex] = binormal;
                    previousTangent = tangent;
                    previousNormal = normal;
                    previousBinormal = binormal;
                }
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
        private struct BuildTubeVerticesJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float3> SampleCenters;
            [ReadOnly] public NativeArray<float3> SampleNormals;
            [ReadOnly] public NativeArray<float3> SampleBinormals;
            public NativeArray<TubeVertex> Vertices;
            public float Radius;
            public int RadialSegments;
            public int VerticesPerLink;

            public void Execute(int index)
            {
                int linkIndex = index / VerticesPerLink;
                int vertexInLink = index - (linkIndex * VerticesPerLink);
                int sampleIndex = vertexInLink / RadialSegments;
                int radialIndex = vertexInLink - (sampleIndex * RadialSegments);
                int sampleArrayIndex = linkIndex * SamplesPerLink + sampleIndex;

                float3 center = SampleCenters[sampleArrayIndex];
                float3 normal = SampleNormals[sampleArrayIndex];
                float3 binormal = SampleBinormals[sampleArrayIndex];

                float angle = radialIndex * (TwoPi / RadialSegments);
                math.sincos(angle, out float sinAngle, out float cosAngle);
                float3 radialDirection = (normal * cosAngle) + (binormal * sinAngle);

                Vertices[index] = new TubeVertex
                {
                    Position = center + radialDirection * Radius,
                    Normal = radialDirection
                };
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
        private struct BuildTubeIndicesJob : IJobParallelFor
        {
            public NativeArray<int> Indices;
            public int RadialSegments;
            public int VerticesPerLink;
            public int QuadsPerLink;

            public void Execute(int quadIndex)
            {
                int linkIndex = quadIndex / QuadsPerLink;
                int quadInLink = quadIndex - (linkIndex * QuadsPerLink);
                int ringIndex = quadInLink / RadialSegments;
                int radialIndex = quadInLink - (ringIndex * RadialSegments);

                int baseVertex = linkIndex * VerticesPerLink;
                int ringA = baseVertex + ringIndex * RadialSegments;
                int ringB = ringA + RadialSegments;
                int nextRadial = radialIndex + 1;
                if (nextRadial >= RadialSegments)
                    nextRadial = 0;

                int v00 = ringA + radialIndex;
                int v01 = ringA + nextRadial;
                int v10 = ringB + radialIndex;
                int v11 = ringB + nextRadial;

                int writeIndex = quadIndex * 6;
                Indices[writeIndex] = v00;
                Indices[writeIndex + 1] = v10;
                Indices[writeIndex + 2] = v11;
                Indices[writeIndex + 3] = v00;
                Indices[writeIndex + 4] = v11;
                Indices[writeIndex + 5] = v01;
            }
        }

        private sealed class BatchState
        {
            // COLD ALLOC: Dictionary<long, LinkRegistration>[100] — active link registry per visual batch — owner: ConnectionSplineBatchRenderer.BatchState
            public readonly Dictionary<long, LinkRegistration> Registrations = new Dictionary<long, LinkRegistration>(DefaultBatchCapacity);

            public Mesh Mesh;
            public MeshFilter Filter;
            public MeshRenderer Renderer;
            public Material Material;
            public NativeArray<float3> StartPoints;
            public NativeArray<float3> EndPoints;
            public NativeArray<float3> SampleCenters;
            public NativeArray<float3> SampleNormals;
            public NativeArray<float3> SampleBinormals;
            public NativeArray<TubeVertex> VertexFront;
            public NativeArray<TubeVertex> VertexBack;
            public NativeArray<int> Indices;
            public JobHandle FrameBuildHandle;
            public JobHandle VertexBuildHandle;
            public JobHandle IndexBuildHandle;
            public bool BuildPending;
            public bool Dirty;
            public int ActiveLinkCount;
            public int GeneratedVertexCount;
            public int GeneratedTriangleCount;
            public Color Color;
            public float Radius;
            public BatchKind Kind;
            public int RadialSegments;
            public int VerticesPerLink;
            public int IndicesPerLink;
            public int QuadsPerLink;
        }

        private static ConnectionSplineBatchRenderer _instance;

        // COLD ALLOC: BatchState[3] — persistent shared tube-render batches for pipes and relay cables — owner: ConnectionSplineBatchRenderer
        private readonly BatchState[] _batches = new BatchState[4];

        public static void SubmitPipeLink(long linkId, Vector3 start, Vector3 end, Color color)
        {
            ConnectionSplineBatchRenderer instance = ResolveInstance();
            BatchState activeBatch = instance.ResolvePipeBatch(start, end);
            BatchState inactiveBatch = activeBatch.Kind == BatchKind.PipesNear
                ? instance._batches[(int)BatchKind.PipesFar]
                : instance._batches[(int)BatchKind.PipesNear];
            instance.RemoveLink(inactiveBatch, linkId);
            instance.UpsertLink(activeBatch, linkId, start, end, color);
        }

        public static void RemovePipeLink(long linkId)
        {
            if (_instance == null)
                return;

            _instance.RemoveLink(_instance._batches[(int)BatchKind.PipesNear], linkId);
            _instance.RemoveLink(_instance._batches[(int)BatchKind.PipesFar], linkId);
        }

        public static void SubmitRelayLink(long linkId, Vector3 start, Vector3 end, bool hasPower, Color poweredColor, Color unpoweredColor)
        {
            ConnectionSplineBatchRenderer instance = ResolveInstance();
            instance._batches[(int)BatchKind.RelayPowered].Color = poweredColor;
            instance._batches[(int)BatchKind.RelayUnpowered].Color = unpoweredColor;

            BatchState activeBatch = instance._batches[hasPower ? (int)BatchKind.RelayPowered : (int)BatchKind.RelayUnpowered];
            BatchState inactiveBatch = instance._batches[hasPower ? (int)BatchKind.RelayUnpowered : (int)BatchKind.RelayPowered];
            instance.RemoveLink(inactiveBatch, linkId);
            instance.UpsertLink(activeBatch, linkId, start, end, hasPower ? poweredColor : unpoweredColor);
        }

        public static void RemoveRelayLink(long linkId)
        {
            if (_instance == null)
                return;

            _instance.RemoveLink(_instance._batches[(int)BatchKind.RelayPowered], linkId);
            _instance.RemoveLink(_instance._batches[(int)BatchKind.RelayUnpowered], linkId);
        }

        private static ConnectionSplineBatchRenderer ResolveInstance()
        {
            if (_instance != null)
                return _instance;

            GameObject root = new GameObject("ConnectionSplineBatchRenderer")
            {
                hideFlags = HideFlags.DontSave
            };

            _instance = root.AddComponent<ConnectionSplineBatchRenderer>();
            return _instance;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            InitializeBatch((int)BatchKind.PipesNear, BatchKind.PipesNear, new Color(0.30f, 0.82f, 0.95f, 0.88f), PipeRadiusMeters, NearPipeRadialSegments);
            InitializeBatch((int)BatchKind.PipesFar, BatchKind.PipesFar, new Color(0.30f, 0.82f, 0.95f, 0.88f), PipeRadiusMeters, FarPipeRadialSegments);
            InitializeBatch((int)BatchKind.RelayPowered, BatchKind.RelayPowered, new Color(0.25f, 0.95f, 1f, 0.95f), RelayRadiusMeters, RelayRadialSegments);
            InitializeBatch((int)BatchKind.RelayUnpowered, BatchKind.RelayUnpowered, new Color(0.35f, 0.42f, 0.48f, 0.55f), RelayRadiusMeters, RelayRadialSegments);
        }

        private void LateUpdate()
        {
            for (int batchIndex = 0; batchIndex < _batches.Length; batchIndex++)
                ProcessBatch(_batches[batchIndex]);
        }

        private void OnDestroy()
        {
            for (int batchIndex = 0; batchIndex < _batches.Length; batchIndex++)
                DisposeBatch(_batches[batchIndex]);

            if (_instance == this)
                _instance = null;
        }

        private void InitializeBatch(int index, BatchKind kind, Color color, float radius, int radialSegments)
        {
            int safeRadialSegments = math.max(3, radialSegments);
            BatchState batch = new BatchState
            {
                Kind = kind,
                Color = color,
                Radius = radius,
                RadialSegments = safeRadialSegments,
                VerticesPerLink = SamplesPerLink * safeRadialSegments,
                IndicesPerLink = (SamplesPerLink - 1) * safeRadialSegments * 6,
                QuadsPerLink = (SamplesPerLink - 1) * safeRadialSegments
            };

            GameObject child = new GameObject(((BatchKind)index).ToString())
            {
                hideFlags = HideFlags.DontSave
            };
            child.transform.SetParent(transform, false);

            batch.Filter = child.AddComponent<MeshFilter>();
            batch.Renderer = child.AddComponent<MeshRenderer>();
            batch.Mesh = new Mesh
            {
                name = $"MSH_{((BatchKind)index)}_SplineTubes"
            };
            batch.Mesh.MarkDynamic();
            batch.Filter.sharedMesh = batch.Mesh;
            batch.Material = CreateRuntimeMaterial(color);
            batch.Renderer.sharedMaterial = batch.Material;
            batch.Renderer.shadowCastingMode = ShadowCastingMode.Off;
            batch.Renderer.receiveShadows = false;
            batch.Renderer.enabled = false;

            EnsureBatchCapacity(batch, DefaultBatchCapacity);
            _batches[index] = batch;
        }

        private BatchState ResolvePipeBatch(Vector3 start, Vector3 end)
        {
            Transform playerTransform = BootstrapState.CurrentPlayerTransform;
            if (playerTransform == null)
                return _batches[(int)BatchKind.PipesNear];

            float3 midpoint = ((float3)start + (float3)end) * 0.5f;
            float distanceSq = math.lengthsq(midpoint - (float3)playerTransform.position);
            return distanceSq > PipeLodDistanceMetersSq
                ? _batches[(int)BatchKind.PipesFar]
                : _batches[(int)BatchKind.PipesNear];
        }

        private static Material CreateRuntimeMaterial(Color color)
        {
            Shader shader = Shader.Find(PrimaryShaderName);
            if (shader == null)
                shader = Shader.Find(FallbackShaderName);

            if (shader == null)
                return null;

            Material material = new Material(shader)
            {
                name = "MAT_Runtime_ConnectionSplineBatch",
                hideFlags = HideFlags.DontSave
            };
            ApplyMaterialColor(material, color);
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", 0.15f);
            if (material.HasProperty("_Metallic"))
                material.SetFloat("_Metallic", 0f);

            return material;
        }

        private static void ApplyMaterialColor(Material material, Color color)
        {
            if (material == null)
                return;

            material.color = color;
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
        }

        private void UpsertLink(BatchState batch, long linkId, Vector3 start, Vector3 end, Color color)
        {
            if (batch == null)
                return;

            batch.Color = color;
            batch.Registrations[linkId] = new LinkRegistration
            {
                Start = start,
                End = end
            };
            batch.Dirty = true;
        }

        private void RemoveLink(BatchState batch, long linkId)
        {
            if (batch == null)
                return;

            if (!batch.Registrations.Remove(linkId))
                return;

            batch.Dirty = true;
        }

        private void ProcessBatch(BatchState batch)
        {
            if (batch == null)
                return;

            if (batch.BuildPending)
            {
                if (!batch.FrameBuildHandle.IsCompleted || !batch.VertexBuildHandle.IsCompleted || !batch.IndexBuildHandle.IsCompleted)
                    return;

                batch.FrameBuildHandle.Complete();
                batch.VertexBuildHandle.Complete();
                batch.IndexBuildHandle.Complete();
                batch.BuildPending = false;

                NativeArray<TubeVertex> swap = batch.VertexFront;
                batch.VertexFront = batch.VertexBack;
                batch.VertexBack = swap;
                UploadBatchMesh(batch);
            }

            if (!batch.Dirty)
                return;

            ScheduleBatchBuild(batch);
        }

        private void ScheduleBatchBuild(BatchState batch)
        {
            int linkCount = batch.Registrations.Count;
            batch.ActiveLinkCount = linkCount;
            if (linkCount <= 0)
            {
                batch.GeneratedVertexCount = 0;
                batch.GeneratedTriangleCount = 0;
                batch.Mesh.Clear(false);
                batch.Renderer.enabled = false;
                batch.Dirty = false;
                return;
            }

            EnsureBatchCapacity(batch, linkCount);

            int writeIndex = 0;
            Dictionary<long, LinkRegistration>.Enumerator enumerator = batch.Registrations.GetEnumerator();
            while (enumerator.MoveNext())
            {
                LinkRegistration registration = enumerator.Current.Value;
                batch.StartPoints[writeIndex] = registration.Start;
                batch.EndPoints[writeIndex] = registration.End;
                writeIndex++;
            }

            int vertexCount = linkCount * batch.VerticesPerLink;
            int quadCount = linkCount * batch.QuadsPerLink;
            int indexCount = quadCount * 6;
            batch.GeneratedVertexCount = vertexCount;
            batch.GeneratedTriangleCount = indexCount / 3;

            BuildTubeFramesJob frameJob = new BuildTubeFramesJob
            {
                StartPoints = batch.StartPoints,
                EndPoints = batch.EndPoints,
                SampleCenters = batch.SampleCenters,
                SampleNormals = batch.SampleNormals,
                SampleBinormals = batch.SampleBinormals
            };

            BuildTubeVerticesJob vertexJob = new BuildTubeVerticesJob
            {
                SampleCenters = batch.SampleCenters,
                SampleNormals = batch.SampleNormals,
                SampleBinormals = batch.SampleBinormals,
                Vertices = batch.VertexBack,
                Radius = batch.Radius,
                RadialSegments = batch.RadialSegments,
                VerticesPerLink = batch.VerticesPerLink
            };

            BuildTubeIndicesJob indexJob = new BuildTubeIndicesJob
            {
                Indices = batch.Indices,
                RadialSegments = batch.RadialSegments,
                VerticesPerLink = batch.VerticesPerLink,
                QuadsPerLink = batch.QuadsPerLink
            };

            batch.FrameBuildHandle = frameJob.Schedule(linkCount, 1);
            batch.VertexBuildHandle = vertexJob.Schedule(vertexCount, 32, batch.FrameBuildHandle);
            batch.IndexBuildHandle = indexJob.Schedule(quadCount, 32);
            batch.BuildPending = true;
            batch.Dirty = false;
        }

        private void UploadBatchMesh(BatchState batch)
        {
            ApplyMaterialColor(batch.Material, batch.Color);

            int vertexCount = batch.GeneratedVertexCount;
            int indexCount = batch.GeneratedTriangleCount * 3;

            Mesh.MeshDataArray meshDataArray = Mesh.AllocateWritableMeshData(1);
            Mesh.MeshData meshData = meshDataArray[0];
            meshData.SetVertexBufferParams(
                vertexCount,
                new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
                new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3));
            meshData.SetIndexBufferParams(indexCount, IndexFormat.UInt32);
            meshData.GetVertexData<TubeVertex>().CopyFrom(batch.VertexFront.GetSubArray(0, vertexCount));
            meshData.GetIndexData<int>().CopyFrom(batch.Indices.GetSubArray(0, indexCount));
            meshData.subMeshCount = 1;
            meshData.SetSubMesh(0, new SubMeshDescriptor(0, indexCount, MeshTopology.Triangles), MeshUpdateFlags.DontRecalculateBounds);

            batch.Mesh.Clear(false);
            Mesh.ApplyAndDisposeWritableMeshData(
                meshDataArray,
                batch.Mesh,
                MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers);
            batch.Mesh.bounds = ComputeBounds(batch, vertexCount);
            batch.Renderer.enabled = vertexCount > 0 && indexCount > 0 && batch.Material != null;
        }

        private static Bounds ComputeBounds(BatchState batch, int vertexCount)
        {
            if (vertexCount <= 0)
                return new Bounds(Vector3.zero, Vector3.zero);

            float3 min = batch.VertexFront[0].Position;
            float3 max = min;
            for (int vertexIndex = 1; vertexIndex < vertexCount; vertexIndex++)
            {
                float3 position = batch.VertexFront[vertexIndex].Position;
                min = math.min(min, position);
                max = math.max(max, position);
            }

            float3 size = math.max(max - min, new float3(0.02f, 0.02f, 0.02f));
            float3 center = (min + max) * 0.5f;
            return new Bounds(new Vector3(center.x, center.y, center.z), new Vector3(size.x, size.y, size.z));
        }

        private static void EnsureBatchCapacity(BatchState batch, int linkCapacity)
        {
            int safeLinkCapacity = math.max(1, linkCapacity);
            EnsureArrayCapacity(ref batch.StartPoints, safeLinkCapacity);
            EnsureArrayCapacity(ref batch.EndPoints, safeLinkCapacity);
            EnsureArrayCapacity(ref batch.SampleCenters, safeLinkCapacity * SamplesPerLink);
            EnsureArrayCapacity(ref batch.SampleNormals, safeLinkCapacity * SamplesPerLink);
            EnsureArrayCapacity(ref batch.SampleBinormals, safeLinkCapacity * SamplesPerLink);
            EnsureArrayCapacity(ref batch.VertexFront, safeLinkCapacity * batch.VerticesPerLink);
            EnsureArrayCapacity(ref batch.VertexBack, safeLinkCapacity * batch.VerticesPerLink);
            EnsureArrayCapacity(ref batch.Indices, safeLinkCapacity * batch.IndicesPerLink);
        }

        private static void EnsureArrayCapacity(ref NativeArray<float3> array, int requiredLength)
        {
            if (array.IsCreated && array.Length >= requiredLength)
                return;

            if (array.IsCreated)
                array.Dispose();

            array = new NativeArray<float3>(requiredLength, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        }

        private static void EnsureArrayCapacity(ref NativeArray<TubeVertex> array, int requiredLength)
        {
            if (array.IsCreated && array.Length >= requiredLength)
                return;

            if (array.IsCreated)
                array.Dispose();

            array = new NativeArray<TubeVertex>(requiredLength, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        }

        private static void EnsureArrayCapacity(ref NativeArray<int> array, int requiredLength)
        {
            if (array.IsCreated && array.Length >= requiredLength)
                return;

            if (array.IsCreated)
                array.Dispose();

            array = new NativeArray<int>(requiredLength, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        }

        private static void DisposeBatch(BatchState batch)
        {
            if (batch == null)
                return;

            if (batch.BuildPending)
            {
                batch.FrameBuildHandle.Complete();
                batch.VertexBuildHandle.Complete();
                batch.IndexBuildHandle.Complete();
                batch.BuildPending = false;
            }

            if (batch.StartPoints.IsCreated)
                batch.StartPoints.Dispose();

            if (batch.EndPoints.IsCreated)
                batch.EndPoints.Dispose();

            if (batch.SampleCenters.IsCreated)
                batch.SampleCenters.Dispose();

            if (batch.SampleNormals.IsCreated)
                batch.SampleNormals.Dispose();

            if (batch.SampleBinormals.IsCreated)
                batch.SampleBinormals.Dispose();

            if (batch.VertexFront.IsCreated)
                batch.VertexFront.Dispose();

            if (batch.VertexBack.IsCreated)
                batch.VertexBack.Dispose();

            if (batch.Indices.IsCreated)
                batch.Indices.Dispose();

            if (batch.Mesh != null)
                Destroy(batch.Mesh);

            if (batch.Material != null)
                Destroy(batch.Material);
        }

        private static void ResolveSplineControlPoints(float3 start, float3 end, out float3 p0, out float3 p1, out float3 p2, out float3 p3)
        {
            float3 delta = end - start;
            float length = math.length(delta);
            float archHeight = math.min(length * 0.12f, 0.75f);
            float3 arch = new float3(0f, archHeight, 0f);
            p0 = start - delta * 0.25f + arch * 0.35f;
            p1 = start;
            p2 = end;
            p3 = end + delta * 0.25f + arch * 0.35f;
        }

        private static float3 EvaluateCatmullRom(float3 p0, float3 p1, float3 p2, float3 p3, float t)
        {
            float tt = t * t;
            float ttt = tt * t;
            return 0.5f * (
                (2f * p1) +
                (-p0 + p2) * t +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * tt +
                (-p0 + 3f * p1 - 3f * p2 + p3) * ttt);
        }

        private static float3 EvaluateCatmullRomTangent(float3 p0, float3 p1, float3 p2, float3 p3, float t)
        {
            float tt = t * t;
            return 0.5f * (
                (-p0 + p2) +
                (2f * (2f * p0 - 5f * p1 + 4f * p2 - p3)) * t +
                (3f * (-p0 + 3f * p1 - 3f * p2 + p3)) * tt);
        }

        private static void ResolveInitialFrame(float3 tangent, out float3 normal, out float3 binormal)
        {
            float3 referenceUp = math.abs(tangent.y) > 0.98f ? new float3(1f, 0f, 0f) : new float3(0f, 1f, 0f);
            normal = SafeNormalize(referenceUp - tangent * math.dot(referenceUp, tangent), new float3(0f, 0f, 1f));
            binormal = SafeNormalize(math.cross(tangent, normal), new float3(1f, 0f, 0f));
            normal = SafeNormalize(math.cross(binormal, tangent), new float3(0f, 1f, 0f));
        }

        private static void TransportFrame(float3 previousTangent, float3 currentTangent, float3 previousNormal, float3 previousBinormal, out float3 normal, out float3 binormal)
        {
            float3 rotationAxis = math.cross(previousTangent, currentTangent);
            float axisLengthSq = math.lengthsq(rotationAxis);
            float tangentDot = math.clamp(math.dot(previousTangent, currentTangent), -1f, 1f);

            if (axisLengthSq <= 0.000001f)
            {
                if (tangentDot < -0.9999f)
                {
                    float3 fallbackAxis = math.abs(previousTangent.y) > 0.98f ? new float3(1f, 0f, 0f) : new float3(0f, 1f, 0f);
                    rotationAxis = SafeNormalize(math.cross(previousTangent, fallbackAxis), new float3(0f, 0f, 1f));
                    normal = RotateAroundAxis(previousNormal, rotationAxis, -1f, 0f);
                    binormal = SafeNormalize(math.cross(currentTangent, normal), previousBinormal);
                    normal = SafeNormalize(math.cross(binormal, currentTangent), previousNormal);
                    return;
                }

                normal = previousNormal;
                binormal = previousBinormal;
                return;
            }

            float axisLength = math.sqrt(axisLengthSq);
            float3 axis = rotationAxis / axisLength;
            normal = RotateAroundAxis(previousNormal, axis, tangentDot, axisLength);
            binormal = RotateAroundAxis(previousBinormal, axis, tangentDot, axisLength);
            normal = SafeNormalize(normal - currentTangent * math.dot(normal, currentTangent), previousNormal);
            binormal = SafeNormalize(math.cross(currentTangent, normal), previousBinormal);
            normal = SafeNormalize(math.cross(binormal, currentTangent), previousNormal);
        }

        private static float3 RotateAroundAxis(float3 vector, float3 axis, float cosTheta, float sinTheta)
        {
            return vector * cosTheta +
                   math.cross(axis, vector) * sinTheta +
                   axis * math.dot(axis, vector) * (1f - cosTheta);
        }

        private static float3 SafeNormalize(float3 value, float3 fallback)
        {
            float lengthSq = math.lengthsq(value);
            if (lengthSq <= 0.000001f)
                return fallback;

            return value * math.rsqrt(lengthSq);
        }
    }
}
