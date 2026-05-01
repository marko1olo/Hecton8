using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
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
    public sealed class ConnectionSplineBatchRenderer : MonoBehaviour, ILateFrameTickable
    {
        private const int SamplesPerLink = 8;
        private const int NearPipeRadialSegments = 8;
        private const int FarPipeRadialSegments = 4;
        private const int RelayRadialSegments = 8;
        private const int DefaultBatchCapacity = 100;
        private const string PrimaryShaderName = "Universal Render Pipeline/Lit";
        private const string FallbackShaderName = "Standard";
        private const float PipeLodRefreshThresholdMetersSq = 1f;
        private const float RelayRadiusMeters = 0.028f;
        private const float TwoPi = math.PI * 2f;
        private const float RuptureBucklingFrequency = 15f;
        private const float RuptureBucklingAmplitude = 0.15f;

        private enum BatchKind : byte
        {
            PipesNear = 0,
            PipesFar = 1,
            PipesLine = 2,
            RelayPowered = 3,
            RelayUnpowered = 4
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct TubeVertex
        {
            public float3 Position;
            public float3 Normal;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct LineVertex
        {
            public float3 Position;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct BuildTubeFramesJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<SplineDescriptor> Descriptors;
            public NativeArray<float3> SampleCenters;
            public NativeArray<float3> SampleNormals;
            public NativeArray<float3> SampleBinormals;

            public void Execute(int linkIndex)
            {
                SplineDescriptor descriptor = Descriptors[linkIndex];
                LogisticsPipeBuilder.ResolveControlPoints(in descriptor, out float3 p0, out float3 p1, out float3 p2, out float3 p3);

                int sampleBaseIndex = linkIndex * SamplesPerLink;
                float3 previousTangent = new float3(0f, 1f, 0f);
                float3 previousNormal = new float3(0f, 0f, 1f);
                float3 previousBinormal = new float3(1f, 0f, 0f);

                for (int sampleIndex = 0; sampleIndex < SamplesPerLink; sampleIndex++)
                {
                    float t = SamplesPerLink > 1 ? sampleIndex / (float)(SamplesPerLink - 1) : 0f;
                    float3 center = LogisticsPipeBuilder.EvaluateSpline(p0, p1, p2, p3, t);
                    float3 tangent = LogisticsPipeBuilder.SafeNormalize(
                        LogisticsPipeBuilder.EvaluateTangent(p0, p1, p2, p3, t),
                        new float3(0f, 1f, 0f));

                    float3 normal;
                    float3 binormal;
                    if (sampleIndex == 0)
                    {
                        LogisticsPipeBuilder.ResolveInitialFrame(tangent, out normal, out binormal);
                    }
                    else
                    {
                        LogisticsPipeBuilder.TransportFrame(previousTangent, tangent, previousNormal, previousBinormal, out normal, out binormal);
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

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct BuildTubeVerticesJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<SplineDescriptor> Descriptors;
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
                float3 position = center + radialDirection * Radius;

                if (LogisticsPipeBuilder.HasRupturedMask(Descriptors[linkIndex].Flags))
                {
                    float ruptureHash = ResolveRuptureHash(linkIndex);
                    position += radialDirection * math.sin(position.z * RuptureBucklingFrequency + ruptureHash) * RuptureBucklingAmplitude;
                }

                Vertices[index] = new TubeVertex
                {
                    Position = position,
                    Normal = radialDirection
                };
            }

            private static float ResolveRuptureHash(int linkIndex)
            {
                float phase = (linkIndex + 1) * 12.9898f;
                return math.frac(math.sin(phase) * 43758.5453f) * TwoPi;
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
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
            // COLD ALLOC: Dictionary<long,SplineDescriptor>[100] — active link registry per visual batch — owner: ConnectionSplineBatchRenderer.BatchState
            public readonly Dictionary<long, SplineDescriptor> Registrations = new Dictionary<long, SplineDescriptor>(DefaultBatchCapacity);

            public Mesh Mesh;
            public MeshFilter Filter;
            public MeshRenderer Renderer;
            public Material Material;
            public NativeArray<SplineDescriptor> Descriptors;
            public NativeArray<float3> SampleCenters;
            public NativeArray<float3> SampleNormals;
            public NativeArray<float3> SampleBinormals;
            public NativeArray<TubeVertex> VertexFront;
            public NativeArray<TubeVertex> VertexBack;
            public NativeArray<LineVertex> LineFront;
            public NativeArray<LineVertex> LineBack;
            public NativeArray<int> Indices;
            public JobHandle FrameBuildHandle;
            public JobHandle VertexBuildHandle;
            public JobHandle IndexBuildHandle;
            public bool BuildPending;
            public bool Dirty;
            public int GeneratedVertexCount;
            public int GeneratedIndexCount;
            public Color Color;
            public float Radius;
            public BatchKind Kind;
            public int RadialSegments;
            public int VerticesPerLink;
            public int IndicesPerLink;
            public int QuadsPerLink;
            public MeshTopology Topology;
        }

        private static ConnectionSplineBatchRenderer _instance;
        private bool _registeredLateFrameTick;

        // COLD ALLOC: BatchState[5] — persistent shared tube/line render batches for pipes and relay cables — owner: ConnectionSplineBatchRenderer
        private readonly BatchState[] _batches = new BatchState[5];
        // COLD ALLOC: Dictionary<long,SplineDescriptor>[100] — master logistics-pipe registry for distance-based batch reassignment — owner: ConnectionSplineBatchRenderer
        private readonly Dictionary<long, SplineDescriptor> _pipeRegistrations = new Dictionary<long, SplineDescriptor>(DefaultBatchCapacity);
        private readonly HashSet<uint> _rupturedPipeNodes = new HashSet<uint>();
        private readonly List<long> _pipeRuptureUpdateScratch = new List<long>(DefaultBatchCapacity);

        private bool _pipeLodDirty = true;
        private float3 _lastPipeObserverPosition;
        private bool _hasLastPipeObserverPosition;

        /// <summary>Compatibility overload for existing point-to-point logistics pipes.</summary>
        public static void SubmitPipeLink(long linkId, Vector3 start, Vector3 end, Color color)
        {
            SplineDescriptor descriptor = LogisticsPipeBuilder.CreateLinearDescriptor(
                start,
                end,
                LogisticsPipeBuilder.DefaultPipeRadiusMeters,
                PipeRenderFlags.None);
            SubmitPipeLink(linkId, descriptor, color);
        }

        internal static void SubmitPipeLink(long linkId, SplineDescriptor descriptor, Color color)
        {
            ConnectionSplineBatchRenderer instance = ResolveInstance();
            instance.UpsertPipeLink(linkId, descriptor, color);
        }

        public static void RemovePipeLink(long linkId)
        {
            if (_instance == null)
                return;

            _instance._pipeRegistrations.Remove(linkId);
            _instance.RemoveLink(_instance._batches[(int)BatchKind.PipesNear], linkId);
            _instance.RemoveLink(_instance._batches[(int)BatchKind.PipesFar], linkId);
            _instance.RemoveLink(_instance._batches[(int)BatchKind.PipesLine], linkId);
            _instance._pipeLodDirty = true;
        }

        internal static void SetPipeNodeRuptured(uint nodeId, bool ruptured)
        {
            if (_instance == null)
                return;

            _instance.SetPipeNodeRupturedInternal(nodeId, ruptured);
        }

        public static void SubmitRelayLink(long linkId, Vector3 start, Vector3 end, bool hasPower, Color poweredColor, Color unpoweredColor)
        {
            ConnectionSplineBatchRenderer instance = ResolveInstance();
            instance._batches[(int)BatchKind.RelayPowered].Color = poweredColor;
            instance._batches[(int)BatchKind.RelayUnpowered].Color = unpoweredColor;

            float3 chordDirection = LogisticsPipeBuilder.SafeNormalize((float3)end - (float3)start, new float3(0f, 0f, 1f));
            SplineDescriptor descriptor = LogisticsPipeBuilder.CreateSocketDescriptor(
                start,
                end,
                chordDirection,
                -chordDirection,
                RelayRadiusMeters,
                PipeRenderFlags.None);

            BatchState activeBatch = instance._batches[hasPower ? (int)BatchKind.RelayPowered : (int)BatchKind.RelayUnpowered];
            BatchState inactiveBatch = instance._batches[hasPower ? (int)BatchKind.RelayUnpowered : (int)BatchKind.RelayPowered];
            instance.RemoveLink(inactiveBatch, linkId);
            instance.UpsertLink(activeBatch, linkId, descriptor);
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
            Color pipeColor = new Color(0.30f, 0.82f, 0.95f, 0.88f);
            InitializeBatch((int)BatchKind.PipesNear, BatchKind.PipesNear, pipeColor, LogisticsPipeBuilder.DefaultPipeRadiusMeters, NearPipeRadialSegments, MeshTopology.Triangles);
            InitializeBatch((int)BatchKind.PipesFar, BatchKind.PipesFar, pipeColor, LogisticsPipeBuilder.DefaultPipeRadiusMeters, FarPipeRadialSegments, MeshTopology.Triangles);
            InitializeBatch((int)BatchKind.PipesLine, BatchKind.PipesLine, pipeColor, LogisticsPipeBuilder.DefaultPipeRadiusMeters, 0, MeshTopology.Lines);
            InitializeBatch((int)BatchKind.RelayPowered, BatchKind.RelayPowered, new Color(0.25f, 0.95f, 1f, 0.95f), RelayRadiusMeters, RelayRadialSegments, MeshTopology.Triangles);
            InitializeBatch((int)BatchKind.RelayUnpowered, BatchKind.RelayUnpowered, new Color(0.35f, 0.42f, 0.48f, 0.55f), RelayRadiusMeters, RelayRadialSegments, MeshTopology.Triangles);
        }

        private void OnEnable()
        {
            if (_registeredLateFrameTick || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterLateFrameTickable(this, PriorityLayer.Environment);
            _registeredLateFrameTick = true;
        }

        private void OnDisable()
        {
            if (!_registeredLateFrameTick)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
            _registeredLateFrameTick = false;
        }

        /// <inheritdoc />
        public void LateFrameTick()
        {
            RefreshPipeBatchAssignments();
            for (int batchIndex = 0; batchIndex < _batches.Length; batchIndex++)
                ProcessBatch(_batches[batchIndex]);
        }

        private void OnDestroy()
        {
            OnDisable();

            for (int batchIndex = 0; batchIndex < _batches.Length; batchIndex++)
                DisposeBatch(_batches[batchIndex]);

            if (_instance == this)
                _instance = null;
        }

        private void InitializeBatch(int index, BatchKind kind, Color color, float radius, int radialSegments, MeshTopology topology)
        {
            int safeRadialSegments = topology == MeshTopology.Triangles ? math.max(3, radialSegments) : 0;
            BatchState batch = new BatchState
            {
                Kind = kind,
                Color = color,
                Radius = radius,
                RadialSegments = safeRadialSegments,
                VerticesPerLink = topology == MeshTopology.Triangles ? SamplesPerLink * safeRadialSegments : 2,
                IndicesPerLink = topology == MeshTopology.Triangles ? (SamplesPerLink - 1) * safeRadialSegments * 6 : 2,
                QuadsPerLink = topology == MeshTopology.Triangles ? (SamplesPerLink - 1) * safeRadialSegments : 0,
                Topology = topology
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
                name = $"MSH_{((BatchKind)index)}_SplineBatch"
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

        private void RefreshPipeBatchAssignments()
        {
            float3 observerPosition = ResolveObserverPosition(out bool hasObserver);
            bool observerMoved = hasObserver &&
                                (!_hasLastPipeObserverPosition ||
                                 math.lengthsq(observerPosition - _lastPipeObserverPosition) > PipeLodRefreshThresholdMetersSq);

            if (!_pipeLodDirty && !observerMoved)
                return;

            ClearPipeBatch(_batches[(int)BatchKind.PipesNear]);
            ClearPipeBatch(_batches[(int)BatchKind.PipesFar]);
            ClearPipeBatch(_batches[(int)BatchKind.PipesLine]);

            Dictionary<long, SplineDescriptor>.Enumerator enumerator = _pipeRegistrations.GetEnumerator();
            while (enumerator.MoveNext())
            {
                KeyValuePair<long, SplineDescriptor> pair = enumerator.Current;
                PipeVisualLod lod = hasObserver
                    ? LogisticsPipeBuilder.ResolveVisualLod(pair.Value, observerPosition)
                    : PipeVisualLod.Tube8;

                BatchState targetBatch = ResolvePipeBatch(lod);
                targetBatch.Registrations[pair.Key] = pair.Value;
                targetBatch.Dirty = true;
            }

            _pipeLodDirty = false;
            _lastPipeObserverPosition = observerPosition;
            _hasLastPipeObserverPosition = hasObserver;
        }

        private void ClearPipeBatch(BatchState batch)
        {
            if (batch == null || batch.Registrations.Count <= 0)
                return;

            batch.Registrations.Clear();
            batch.Dirty = true;
        }

        private BatchState ResolvePipeBatch(PipeVisualLod lod)
        {
            switch (lod)
            {
                case PipeVisualLod.Tube4:
                    return _batches[(int)BatchKind.PipesFar];

                case PipeVisualLod.Line:
                    return _batches[(int)BatchKind.PipesLine];

                default:
                    return _batches[(int)BatchKind.PipesNear];
            }
        }

        private float3 ResolveObserverPosition(out bool hasObserver)
        {
            Transform playerTransform = BootstrapState.CurrentPlayerTransform;
            if (playerTransform == null)
            {
                hasObserver = false;
                return float3.zero;
            }

            hasObserver = true;
            return playerTransform.position;
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

        private void UpsertPipeLink(long linkId, SplineDescriptor descriptor, Color color)
        {
            _batches[(int)BatchKind.PipesNear].Color = color;
            _batches[(int)BatchKind.PipesFar].Color = color;
            _batches[(int)BatchKind.PipesLine].Color = color;
            ApplyPipeRuptureFlags(linkId, ref descriptor);
            _pipeRegistrations[linkId] = descriptor;
            _pipeLodDirty = true;
        }

        private void SetPipeNodeRupturedInternal(uint nodeId, bool ruptured)
        {
            bool changed = ruptured
                ? _rupturedPipeNodes.Add(nodeId)
                : _rupturedPipeNodes.Remove(nodeId);

            if (!changed)
                return;

            _pipeRuptureUpdateScratch.Clear();
            Dictionary<long, SplineDescriptor>.Enumerator enumerator = _pipeRegistrations.GetEnumerator();
            while (enumerator.MoveNext())
            {
                if (PipeLinkContainsNode(enumerator.Current.Key, nodeId))
                    _pipeRuptureUpdateScratch.Add(enumerator.Current.Key);
            }

            int updateCount = _pipeRuptureUpdateScratch.Count;
            for (int i = 0; i < updateCount; i++)
            {
                long linkId = _pipeRuptureUpdateScratch[i];
                if (!_pipeRegistrations.TryGetValue(linkId, out SplineDescriptor descriptor))
                    continue;

                ApplyPipeRuptureFlags(linkId, ref descriptor);
                _pipeRegistrations[linkId] = descriptor;
            }

            _pipeLodDirty = true;
        }

        private void ApplyPipeRuptureFlags(long linkId, ref SplineDescriptor descriptor)
        {
            descriptor.Flags &= ~PipeRenderFlags.MaskRuptured;
            DecodePipeLinkId(linkId, out uint leftNodeId, out uint rightNodeId);
            if (_rupturedPipeNodes.Contains(leftNodeId) || _rupturedPipeNodes.Contains(rightNodeId))
                descriptor.Flags |= PipeRenderFlags.MaskRuptured;
        }

        private static bool PipeLinkContainsNode(long linkId, uint nodeId)
        {
            DecodePipeLinkId(linkId, out uint leftNodeId, out uint rightNodeId);
            return leftNodeId == nodeId || rightNodeId == nodeId;
        }

        private static void DecodePipeLinkId(long linkId, out uint leftNodeId, out uint rightNodeId)
        {
            leftNodeId = (uint)(linkId >> 32);
            rightNodeId = unchecked((uint)linkId);
        }

        private void UpsertLink(BatchState batch, long linkId, SplineDescriptor descriptor)
        {
            if (batch == null)
                return;

            batch.Registrations[linkId] = descriptor;
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
                if (!IsBatchBuildCompleted(batch))
                    return;

                CompletePendingBuildIfNeeded(batch);

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
            if (linkCount <= 0)
            {
                batch.GeneratedVertexCount = 0;
                batch.GeneratedIndexCount = 0;
                batch.Mesh.Clear(false);
                batch.Renderer.enabled = false;
                batch.Dirty = false;
                return;
            }

            EnsureBatchCapacity(batch, linkCount);

            int writeIndex = 0;
            Dictionary<long, SplineDescriptor>.Enumerator enumerator = batch.Registrations.GetEnumerator();
            while (enumerator.MoveNext())
            {
                batch.Descriptors[writeIndex] = enumerator.Current.Value;
                writeIndex++;
            }

            if (batch.Topology == MeshTopology.Lines)
            {
                BuildLineBatch(batch, linkCount);
                batch.Dirty = false;
                return;
            }

            int vertexCount = linkCount * batch.VerticesPerLink;
            int quadCount = linkCount * batch.QuadsPerLink;
            int indexCount = quadCount * 6;
            batch.GeneratedVertexCount = vertexCount;
            batch.GeneratedIndexCount = indexCount;

            BuildTubeFramesJob frameJob = new BuildTubeFramesJob
            {
                Descriptors = batch.Descriptors,
                SampleCenters = batch.SampleCenters,
                SampleNormals = batch.SampleNormals,
                SampleBinormals = batch.SampleBinormals
            };

            BuildTubeVerticesJob vertexJob = new BuildTubeVerticesJob
            {
                Descriptors = batch.Descriptors,
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

        private void BuildLineBatch(BatchState batch, int linkCount)
        {
            int vertexCount = linkCount * 2;
            batch.GeneratedVertexCount = vertexCount;
            batch.GeneratedIndexCount = vertexCount;

            for (int linkIndex = 0; linkIndex < linkCount; linkIndex++)
            {
                SplineDescriptor descriptor = batch.Descriptors[linkIndex];
                int vertexBaseIndex = linkIndex * 2;
                batch.LineBack[vertexBaseIndex] = new LineVertex { Position = descriptor.Start };
                batch.LineBack[vertexBaseIndex + 1] = new LineVertex { Position = descriptor.End };
                batch.Indices[vertexBaseIndex] = vertexBaseIndex;
                batch.Indices[vertexBaseIndex + 1] = vertexBaseIndex + 1;
            }

            NativeArray<LineVertex> swap = batch.LineFront;
            batch.LineFront = batch.LineBack;
            batch.LineBack = swap;
            UploadBatchMesh(batch);
        }

        private void UploadBatchMesh(BatchState batch)
        {
            ApplyMaterialColor(batch.Material, batch.Color);

            int vertexCount = batch.GeneratedVertexCount;
            int indexCount = batch.GeneratedIndexCount;

            Mesh.MeshDataArray meshDataArray = Mesh.AllocateWritableMeshData(1);
            Mesh.MeshData meshData = meshDataArray[0];

            if (batch.Topology == MeshTopology.Lines)
            {
                meshData.SetVertexBufferParams(
                    vertexCount,
                    new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3));
                meshData.SetIndexBufferParams(indexCount, IndexFormat.UInt32);
                CopyToMeshBuffer(batch.LineFront, vertexCount, meshData.GetVertexData<LineVertex>());
            }
            else
            {
                meshData.SetVertexBufferParams(
                    vertexCount,
                    new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
                    new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3));
                meshData.SetIndexBufferParams(indexCount, IndexFormat.UInt32);
                CopyToMeshBuffer(batch.VertexFront, vertexCount, meshData.GetVertexData<TubeVertex>());
            }

            CopyToMeshBuffer(batch.Indices, indexCount, meshData.GetIndexData<int>());
            meshData.subMeshCount = 1;
            meshData.SetSubMesh(0, new SubMeshDescriptor(0, indexCount, batch.Topology), MeshUpdateFlags.DontRecalculateBounds);

            batch.Mesh.Clear(false);
            Mesh.ApplyAndDisposeWritableMeshData(
                meshDataArray,
                batch.Mesh,
                MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers);
            batch.Mesh.bounds = ComputeBounds(batch, vertexCount);
            batch.Renderer.enabled = vertexCount > 0 && indexCount > 0 && batch.Material != null;
        }

        private static unsafe void CopyToMeshBuffer<T>(NativeArray<T> source, int count, NativeArray<T> destination)
            where T : unmanaged
        {
            if (count <= 0)
                return;

            void* sourcePtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(source);
            void* destinationPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(destination);
            long copyBytes = (long)count * UnsafeUtility.SizeOf<T>();
            long destinationBytes = (long)destination.Length * UnsafeUtility.SizeOf<T>();
            if (!UnsafeMemoryCopyGuard.TryMemCpy(destinationPtr, destinationBytes, sourcePtr, copyBytes))
                UnsafeMemoryCopyGuard.ReportRejectedCopy(nameof(ConnectionSplineBatchRenderer));
        }

        private static Bounds ComputeBounds(BatchState batch, int vertexCount)
        {
            if (vertexCount <= 0)
                return new Bounds(Vector3.zero, Vector3.zero);

            float3 min;
            float3 max;
            if (batch.Topology == MeshTopology.Lines)
            {
                min = batch.LineFront[0].Position;
                max = min;
                for (int vertexIndex = 1; vertexIndex < vertexCount; vertexIndex++)
                {
                    float3 position = batch.LineFront[vertexIndex].Position;
                    min = math.min(min, position);
                    max = math.max(max, position);
                }
            }
            else
            {
                min = batch.VertexFront[0].Position;
                max = min;
                for (int vertexIndex = 1; vertexIndex < vertexCount; vertexIndex++)
                {
                    float3 position = batch.VertexFront[vertexIndex].Position;
                    min = math.min(min, position);
                    max = math.max(max, position);
                }
            }

            float3 size = math.max(max - min, new float3(0.02f, 0.02f, 0.02f));
            float3 center = (min + max) * 0.5f;
            return new Bounds(new Vector3(center.x, center.y, center.z), new Vector3(size.x, size.y, size.z));
        }

        private static void EnsureBatchCapacity(BatchState batch, int linkCapacity)
        {
            int safeLinkCapacity = math.max(1, linkCapacity);
            CompletePendingBuildIfNeeded(batch);
            EnsureArrayCapacity(ref batch.Descriptors, safeLinkCapacity);
            EnsureArrayCapacity(ref batch.Indices, safeLinkCapacity * batch.IndicesPerLink);

            if (batch.Topology == MeshTopology.Lines)
            {
                EnsureArrayCapacity(ref batch.LineFront, safeLinkCapacity * 2);
                EnsureArrayCapacity(ref batch.LineBack, safeLinkCapacity * 2);
                return;
            }

            EnsureArrayCapacity(ref batch.SampleCenters, safeLinkCapacity * SamplesPerLink);
            EnsureArrayCapacity(ref batch.SampleNormals, safeLinkCapacity * SamplesPerLink);
            EnsureArrayCapacity(ref batch.SampleBinormals, safeLinkCapacity * SamplesPerLink);
            EnsureArrayCapacity(ref batch.VertexFront, safeLinkCapacity * batch.VerticesPerLink);
            EnsureArrayCapacity(ref batch.VertexBack, safeLinkCapacity * batch.VerticesPerLink);
        }

        private static void EnsureArrayCapacity(ref NativeArray<SplineDescriptor> array, int requiredLength)
        {
            if (array.IsCreated && array.Length >= requiredLength)
                return;

            if (array.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(array);
                array.Dispose();
            }

            array = new NativeArray<SplineDescriptor>(requiredLength, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            NativeMemorySentinel.RegisterNativeArray(array, nameof(ConnectionSplineBatchRenderer), nameof(BatchState.Descriptors), NativeAllocationLifetime.Session);
        }

        private static void EnsureArrayCapacity(ref NativeArray<float3> array, int requiredLength)
        {
            if (array.IsCreated && array.Length >= requiredLength)
                return;

            if (array.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(array);
                array.Dispose();
            }

            array = new NativeArray<float3>(requiredLength, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            NativeMemorySentinel.RegisterNativeArray(array, nameof(ConnectionSplineBatchRenderer), "float3BatchArray", NativeAllocationLifetime.Session);
        }

        private static void EnsureArrayCapacity(ref NativeArray<TubeVertex> array, int requiredLength)
        {
            if (array.IsCreated && array.Length >= requiredLength)
                return;

            if (array.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(array);
                array.Dispose();
            }

            array = new NativeArray<TubeVertex>(requiredLength, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            NativeMemorySentinel.RegisterNativeArray(array, nameof(ConnectionSplineBatchRenderer), "tubeVertexBatchArray", NativeAllocationLifetime.Session);
        }

        private static void EnsureArrayCapacity(ref NativeArray<LineVertex> array, int requiredLength)
        {
            if (array.IsCreated && array.Length >= requiredLength)
                return;

            if (array.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(array);
                array.Dispose();
            }

            array = new NativeArray<LineVertex>(requiredLength, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            NativeMemorySentinel.RegisterNativeArray(array, nameof(ConnectionSplineBatchRenderer), "lineVertexBatchArray", NativeAllocationLifetime.Session);
        }

        private static void EnsureArrayCapacity(ref NativeArray<int> array, int requiredLength)
        {
            if (array.IsCreated && array.Length >= requiredLength)
                return;

            if (array.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(array);
                array.Dispose();
            }

            array = new NativeArray<int>(requiredLength, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            NativeMemorySentinel.RegisterNativeArray(array, nameof(ConnectionSplineBatchRenderer), "indexBatchArray", NativeAllocationLifetime.Session);
        }

        private static void DisposeBatch(BatchState batch)
        {
            if (batch == null)
                return;

            CompletePendingBuildIfNeeded(batch);

            if (batch.Descriptors.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(batch.Descriptors);
                batch.Descriptors.Dispose();
            }

            if (batch.SampleCenters.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(batch.SampleCenters);
                batch.SampleCenters.Dispose();
            }

            if (batch.SampleNormals.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(batch.SampleNormals);
                batch.SampleNormals.Dispose();
            }

            if (batch.SampleBinormals.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(batch.SampleBinormals);
                batch.SampleBinormals.Dispose();
            }

            if (batch.VertexFront.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(batch.VertexFront);
                batch.VertexFront.Dispose();
            }

            if (batch.VertexBack.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(batch.VertexBack);
                batch.VertexBack.Dispose();
            }

            if (batch.LineFront.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(batch.LineFront);
                batch.LineFront.Dispose();
            }

            if (batch.LineBack.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(batch.LineBack);
                batch.LineBack.Dispose();
            }

            if (batch.Indices.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(batch.Indices);
                batch.Indices.Dispose();
            }

            if (batch.Mesh != null)
                Destroy(batch.Mesh);

            if (batch.Material != null)
                Destroy(batch.Material);
        }

        private static bool IsBatchBuildCompleted(BatchState batch)
        {
            return batch.FrameBuildHandle.IsCompleted &&
                   batch.VertexBuildHandle.IsCompleted &&
                   batch.IndexBuildHandle.IsCompleted;
        }

        private static void CompletePendingBuildIfNeeded(BatchState batch)
        {
            if (batch == null || !batch.BuildPending)
                return;

            CompleteBuildHandle(ref batch.FrameBuildHandle);
            CompleteBuildHandle(ref batch.VertexBuildHandle);
            CompleteBuildHandle(ref batch.IndexBuildHandle);
            batch.BuildPending = false;
        }

        private static void CompleteBuildHandle(ref JobHandle handle)
        {
            if (!handle.IsCompleted)
            {
                handle.Complete();
                handle = default;
                return;
            }

            handle.Complete();
            handle = default;
        }
    }
}
