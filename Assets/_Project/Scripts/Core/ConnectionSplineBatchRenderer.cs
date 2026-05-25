using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.Core
{
    /// <summary>
    /// Shared runtime renderer for logistics pipes and relay cables.
    /// Uses one static cylinder mesh per visual bucket and bends every instance in the vertex shader.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class ConnectionSplineBatchRenderer : MonoBehaviour, IConnectionSplineBatchRendererService, IServiceHeartbeat, IServiceShutdown, ILateFrameTickable, IOriginShiftListener, IGlobalRegistryHotSwapListener
    {
        private const int DefaultBatchCapacity = 100;
        private const int MaxRenderedLinksPerBatch = 64;
        private const string FlexiblePipeShaderName = "Hecton/FlexiblePipe";
        private const string FallbackShaderName = "Universal Render Pipeline/Lit";
        private const string BuiltinCylinderMeshName = "Cylinder.fbx";
        private const float FarPipeSpanThresholdMetersSq = 40f * 40f;
        private const float RelayRadiusMeters = 0.028f;

        private static readonly int s_FlexiblePipeInstancesId = Shader.PropertyToID("_HectonFlexiblePipeInstances");
        private static readonly int s_BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int s_ColorId = Shader.PropertyToID("_Color");
        private static readonly int s_SmoothnessId = Shader.PropertyToID("_Smoothness");
        private static readonly int s_MetallicId = Shader.PropertyToID("_Metallic");
        private static readonly int s_LogisticsPathHighlightId = Shader.PropertyToID("_HectonLogisticsPathHighlight");
        private static Mesh s_staticCylinderMesh;
        private static IConnectionSplineBatchRendererService s_activeService;

        private enum BatchKind : byte
        {
            PipesNear = 0,
            PipesFar = 1,
            PipesLine = 2,
            RelayPowered = 3,
            RelayUnpowered = 4
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct FlexiblePipeInstanceGpuData
        {
            [FieldOffset(0)] public float4 P0Radius;
            [FieldOffset(16)] public float4 P1Flags;
            [FieldOffset(32)] public float4 P2;
            [FieldOffset(48)] public float4 P3;
        }

        private sealed class BatchState
        {
            // COLD ALLOC: Dictionary<long,SplineDescriptor>[100] - active link registry per visual batch - owner: ConnectionSplineBatchRenderer.BatchState
            internal readonly Dictionary<long, SplineDescriptor> Registrations = new Dictionary<long, SplineDescriptor>(DefaultBatchCapacity);

            internal Mesh Mesh;
            internal Material Material;
            internal NativeArray<SplineDescriptor> Descriptors;
            internal NativeArray<FlexiblePipeInstanceGpuData> InstanceData;
            internal GraphicsBuffer InstanceBuffer;
            internal Bounds WorldBounds;
            internal bool Dirty;
            internal bool MaterialColorDirty;
            internal int InstanceCount;
            internal Color Color;
            internal Color AppliedColor;
            internal float Radius;
            internal BatchKind Kind;
        }

        private bool _registeredLateFrameTick;
        private bool _registeredOriginShiftListener;
        private bool _registeredHotSwapListener;
        private bool _serviceRegistered;
        private bool _shutdownComplete;

        // COLD ALLOC: BatchState[5] - persistent shared shader-bent pipe render batches - owner: ConnectionSplineBatchRenderer
        private readonly BatchState[] _batches = new BatchState[5];
        // COLD ALLOC: Dictionary<long,SplineDescriptor>[100] - master logistics-pipe registry for distance-based batch reassignment - owner: ConnectionSplineBatchRenderer
        private readonly Dictionary<long, SplineDescriptor> _pipeRegistrations = new Dictionary<long, SplineDescriptor>(DefaultBatchCapacity);
        // COLD ALLOC: HashSet<uint>[100] - ruptured logistics-pipe endpoint flags - owner: ConnectionSplineBatchRenderer
        private readonly HashSet<uint> _rupturedPipeNodes = new HashSet<uint>(DefaultBatchCapacity);
        // COLD ALLOC: Dictionary<uint,float>[100] - per-node pipe flow scalar for shader panning - owner: ConnectionSplineBatchRenderer
        private readonly Dictionary<uint, float> _pipeNodeFlow01 = new Dictionary<uint, float>(DefaultBatchCapacity);
        // COLD ALLOC: List<long>[100] - shared dictionary-key scratch for rupture and origin-shift rebases - owner: ConnectionSplineBatchRenderer
        private readonly List<long> _pipeRuptureUpdateScratch = new List<long>(DefaultBatchCapacity);

        public ServiceHeartbeatState HeartbeatState => _serviceRegistered ? ServiceHeartbeatState.Ready : ServiceHeartbeatState.NotStarted;

        public bool IsServiceReady => _serviceRegistered;

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
            IConnectionSplineBatchRendererService renderer = ResolveService();
            if (renderer != null)
                renderer.SubmitPipeLink(linkId, descriptor, color);
        }

        public static void RemovePipeLink(long linkId)
        {
            IConnectionSplineBatchRendererService renderer = ResolveService();
            if (renderer != null)
                renderer.RemovePipeLink(linkId);
        }

        internal static void SetPipeNodeRuptured(uint nodeId, bool ruptured)
        {
            IConnectionSplineBatchRendererService renderer = ResolveService();
            if (renderer != null)
                renderer.SetPipeNodeRuptured(nodeId, ruptured);
        }

        internal static void SetPipeNodeFlow(uint nodeId, float flow01)
        {
            IConnectionSplineBatchRendererService renderer = ResolveService();
            if (renderer != null)
                renderer.SetPipeNodeFlow(nodeId, flow01);
        }

        public static void SetLogisticsPathHighlightActive(bool active)
        {
            Shader.SetGlobalFloat(s_LogisticsPathHighlightId, active ? 1f : 0f);
        }

        public static void SubmitRelayLink(long linkId, Vector3 start, Vector3 end, bool hasPower, Color poweredColor, Color unpoweredColor)
        {
            float3 chordDirection = LogisticsPipeBuilder.SafeNormalize((float3)end - (float3)start, new float3(0f, 0f, 1f));
            SplineDescriptor descriptor = LogisticsPipeBuilder.CreateSocketDescriptor(
                start,
                end,
                chordDirection,
                -chordDirection,
                RelayRadiusMeters,
                PipeRenderFlags.None);

            IConnectionSplineBatchRendererService renderer = ResolveService();
            if (renderer != null)
                renderer.SubmitRelaySpline(linkId, descriptor, hasPower, poweredColor, unpoweredColor);
        }

        internal static void SubmitRelaySpline(long linkId, SplineDescriptor descriptor, bool hasPower, Color poweredColor, Color unpoweredColor)
        {
            IConnectionSplineBatchRendererService renderer = ResolveService();
            if (renderer != null)
                renderer.SubmitRelaySpline(linkId, descriptor, hasPower, poweredColor, unpoweredColor);
        }

        public static void RemoveRelayLink(long linkId)
        {
            IConnectionSplineBatchRendererService renderer = ResolveService();
            if (renderer != null)
                renderer.RemoveRelayLink(linkId);
        }

        private static IConnectionSplineBatchRendererService ResolveService()
        {
            return s_activeService;
        }

        private void Awake()
        {
            ConnectionSplineBatchRenderer activeRuntime = GlobalRegistry.ConnectionSplineBatchRenderer;
            if (activeRuntime != null && activeRuntime != this)
            {
                Destroy(gameObject);
                return;
            }

            Color pipeColor = new Color(0.30f, 0.82f, 0.95f, 0.88f);
            InitializeBatch((int)BatchKind.PipesNear, BatchKind.PipesNear, pipeColor, LogisticsPipeBuilder.DefaultPipeRadiusMeters);
            InitializeBatch((int)BatchKind.PipesFar, BatchKind.PipesFar, pipeColor, LogisticsPipeBuilder.DefaultPipeRadiusMeters);
            InitializeBatch((int)BatchKind.PipesLine, BatchKind.PipesLine, pipeColor, LogisticsPipeBuilder.DefaultPipeRadiusMeters);
            InitializeBatch((int)BatchKind.RelayPowered, BatchKind.RelayPowered, new Color(0.25f, 0.95f, 1f, 0.95f), RelayRadiusMeters);
            InitializeBatch((int)BatchKind.RelayUnpowered, BatchKind.RelayUnpowered, new Color(0.35f, 0.42f, 0.48f, 0.55f), RelayRadiusMeters);
        }

        public void InitializeService()
        {
            if (_serviceRegistered)
                return;

            _shutdownComplete = false;
            GlobalRegistry.RegisterConnectionSplineBatchRendererRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.ConnectionSplineBatchRenderer, this);
            if (_serviceRegistered)
                s_activeService = this;
            EnsureRuntimeRegistrations();
        }

        private void OnEnable()
        {
            EnsureRuntimeRegistrations();
        }

        private void OnDisable()
        {
            TryUnregisterOriginShiftListener();
            TryUnregisterLateFrameTickable();
            TryUnregisterHotSwapListener();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher)
                return;

            TryUnregisterLateFrameTickable();
            if (currentService != null && isActiveAndEnabled)
                RefreshLateFrameTickRegistration();
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            Vector3 shiftOffset = shiftData.ShiftOffset;
            float3 shiftOffset3 = new float3(shiftOffset.x, shiftOffset.y, shiftOffset.z);
            if (!math.all(math.isfinite(shiftOffset3)) || math.lengthsq(shiftOffset3) <= 0.000001f)
                return;

            RebaseRegistrationDictionary(_pipeRegistrations, shiftOffset3);
            for (int batchIndex = 0; batchIndex < _batches.Length; batchIndex++)
                RebaseBatchForOriginShift(_batches[batchIndex], shiftOffset3);

            RefreshLateFrameTickRegistration();
        }

        private void EnsureRuntimeRegistrations()
        {
            if (!Application.isPlaying)
                return;

            TryRegisterHotSwapListener();
            TryRegisterOriginShiftListener();
            RefreshLateFrameTickRegistration();
        }

        private void TryRegisterLateFrameTickable()
        {
            if (_registeredLateFrameTick || GlobalRegistry.Dispatcher == null)
                return;

            _registeredLateFrameTick = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregisterLateFrameTickable()
        {
            if (!_registeredLateFrameTick)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
            _registeredLateFrameTick = false;
        }

        private void TryRegisterOriginShiftListener()
        {
            if (_registeredOriginShiftListener)
                return;

            HectonFloatingOrigin.RegisterListener(this);
            _registeredOriginShiftListener = HectonFloatingOrigin.IsListenerRegistered(this);
        }

        private void TryUnregisterOriginShiftListener()
        {
            if (!_registeredOriginShiftListener)
                return;

            HectonFloatingOrigin.UnregisterListener(this);
            _registeredOriginShiftListener = false;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwapListener || !Application.isPlaying)
                return;

            _registeredHotSwapListener = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwapListener)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwapListener = false;
        }

        void IConnectionSplineBatchRendererService.SubmitPipeLink(long linkId, SplineDescriptor descriptor, Color color)
        {
            EnsureRuntimeRegistrations();
            UpsertPipeLink(linkId, descriptor, color);
        }

        void IConnectionSplineBatchRendererService.RemovePipeLink(long linkId)
        {
            _pipeRegistrations.Remove(linkId);
            RemoveLink(_batches[(int)BatchKind.PipesNear], linkId);
            RemoveLink(_batches[(int)BatchKind.PipesFar], linkId);
            RemoveLink(_batches[(int)BatchKind.PipesLine], linkId);
        }

        void IConnectionSplineBatchRendererService.SetPipeNodeRuptured(uint nodeId, bool ruptured)
        {
            SetPipeNodeRupturedInternal(nodeId, ruptured);
        }

        void IConnectionSplineBatchRendererService.SetPipeNodeFlow(uint nodeId, float flow01)
        {
            SetPipeNodeFlowInternal(nodeId, flow01);
        }

        void IConnectionSplineBatchRendererService.SubmitRelaySpline(
            long linkId,
            SplineDescriptor descriptor,
            bool hasPower,
            Color poweredColor,
            Color unpoweredColor)
        {
            EnsureRuntimeRegistrations();
            SetBatchColor(_batches[(int)BatchKind.RelayPowered], poweredColor);
            SetBatchColor(_batches[(int)BatchKind.RelayUnpowered], unpoweredColor);
            BatchState activeBatch = _batches[hasPower ? (int)BatchKind.RelayPowered : (int)BatchKind.RelayUnpowered];
            BatchState inactiveBatch = _batches[hasPower ? (int)BatchKind.RelayUnpowered : (int)BatchKind.RelayPowered];
            RemoveLink(inactiveBatch, linkId);
            UpsertLink(activeBatch, linkId, descriptor);
        }

        void IConnectionSplineBatchRendererService.RemoveRelayLink(long linkId)
        {
            RemoveLink(_batches[(int)BatchKind.RelayPowered], linkId);
            RemoveLink(_batches[(int)BatchKind.RelayUnpowered], linkId);
        }

        public void LateFrameTick()
        {
            for (int batchIndex = 0; batchIndex < _batches.Length; batchIndex++)
                ProcessBatch(_batches[batchIndex]);

            if (!HasRenderableBatchWork() && _registeredLateFrameTick)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLateFrameTick = false;
            }
        }

        private void OnDestroy()
        {
            ShutdownServiceState();
        }

        public void OnServiceShutdown()
        {
            ShutdownServiceState();
        }

        private void ShutdownServiceState()
        {
            if (_shutdownComplete)
                return;

            TryUnregisterOriginShiftListener();
            TryUnregisterLateFrameTickable();
            TryUnregisterHotSwapListener();

            for (int batchIndex = 0; batchIndex < _batches.Length; batchIndex++)
                DisposeBatch(_batches[batchIndex]);

            _pipeRegistrations.Clear();
            _rupturedPipeNodes.Clear();
            _pipeRuptureUpdateScratch.Clear();

            if (_serviceRegistered && ReferenceEquals(GlobalRegistry.ConnectionSplineBatchRenderer, this))
                GlobalRegistry.UnregisterConnectionSplineBatchRendererRuntime(this);

            if (ReferenceEquals(s_activeService, this))
                s_activeService = null;

            _serviceRegistered = false;
            _shutdownComplete = true;
        }

        private void InitializeBatch(int index, BatchKind kind, Color color, float radius)
        {
            BatchState batch = new BatchState
            {
                Kind = kind,
                Color = color,
                AppliedColor = color,
                Radius = radius,
                Mesh = ResolveStaticCylinderMesh(),
                Material = CreateRuntimeMaterial(color),
                Dirty = false,
                MaterialColorDirty = false
            };

            EnsureBatchCapacity(batch, MaxRenderedLinksPerBatch);
            _batches[index] = batch;
        }

        private BatchState ResolvePipeBatch(in SplineDescriptor descriptor)
        {
            float spanSq = math.lengthsq(descriptor.End - descriptor.Start);
            return spanSq > FarPipeSpanThresholdMetersSq
                ? _batches[(int)BatchKind.PipesFar]
                : _batches[(int)BatchKind.PipesNear];
        }

        private void ReassignPipeBatch(long linkId, in SplineDescriptor descriptor)
        {
            BatchState nearBatch = _batches[(int)BatchKind.PipesNear];
            BatchState farBatch = _batches[(int)BatchKind.PipesFar];
            BatchState lineBatch = _batches[(int)BatchKind.PipesLine];
            BatchState targetBatch = ResolvePipeBatch(in descriptor);

            if (ReferenceEquals(targetBatch, nearBatch))
                UpsertLink(nearBatch, linkId, descriptor);
            else
                RemoveLink(nearBatch, linkId);

            if (ReferenceEquals(targetBatch, farBatch))
                UpsertLink(farBatch, linkId, descriptor);
            else
                RemoveLink(farBatch, linkId);

            RemoveLink(lineBatch, linkId);
        }

        private static Material CreateRuntimeMaterial(Color color)
        {
            Shader shader = Shader.Find(FlexiblePipeShaderName);
            if (shader == null)
                shader = Shader.Find(FallbackShaderName);

            if (shader == null)
                return null;

            Material material = new Material(shader)
            {
                name = "MAT_Runtime_FlexiblePipeBatch",
                hideFlags = HideFlags.DontSave
            };
            ApplyMaterialColor(material, color);
            if (material.HasProperty(s_SmoothnessId))
                material.SetFloat(s_SmoothnessId, 0.22f);
            if (material.HasProperty(s_MetallicId))
                material.SetFloat(s_MetallicId, 0f);

            return material;
        }

        private static void ApplyMaterialColor(Material material, Color color)
        {
            if (material == null)
                return;

            material.color = color;
            if (material.HasProperty(s_BaseColorId))
                material.SetColor(s_BaseColorId, color);
            if (material.HasProperty(s_ColorId))
                material.SetColor(s_ColorId, color);
        }

        private void UpsertPipeLink(long linkId, SplineDescriptor descriptor, Color color)
        {
            SetBatchColor(_batches[(int)BatchKind.PipesNear], color);
            SetBatchColor(_batches[(int)BatchKind.PipesFar], color);
            SetBatchColor(_batches[(int)BatchKind.PipesLine], color);
            ApplyPipeDynamicFlags(linkId, ref descriptor);
            _pipeRegistrations[linkId] = descriptor;
            ReassignPipeBatch(linkId, in descriptor);
        }

        private void SetPipeNodeRupturedInternal(uint nodeId, bool ruptured)
        {
            bool changed = ruptured
                ? _rupturedPipeNodes.Add(nodeId)
                : _rupturedPipeNodes.Remove(nodeId);

            if (!changed)
                return;

            UpdatePipeLinksForNode(nodeId);
        }

        private void SetPipeNodeFlowInternal(uint nodeId, float flow01)
        {
            float sanitizedFlow = math.saturate(flow01);
            if (sanitizedFlow <= 0.001f)
            {
                if (!_pipeNodeFlow01.Remove(nodeId))
                    return;
            }
            else
            {
                if (_pipeNodeFlow01.TryGetValue(nodeId, out float previous) && math.abs(previous - sanitizedFlow) <= 0.01f)
                    return;

                _pipeNodeFlow01[nodeId] = sanitizedFlow;
            }

            UpdatePipeLinksForNode(nodeId);
        }

        private void UpdatePipeLinksForNode(uint nodeId)
        {
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

                ApplyPipeDynamicFlags(linkId, ref descriptor);
                _pipeRegistrations[linkId] = descriptor;
                ReassignPipeBatch(linkId, in descriptor);
            }
        }

        private void ApplyPipeDynamicFlags(long linkId, ref SplineDescriptor descriptor)
        {
            descriptor.Flags &= ~(PipeRenderFlags.MaskRuptured | PipeRenderFlags.MaskHasFluidFlow);
            descriptor.FlowScalar = 0f;
            DecodePipeLinkId(linkId, out uint leftNodeId, out uint rightNodeId);
            if (_rupturedPipeNodes.Contains(leftNodeId) || _rupturedPipeNodes.Contains(rightNodeId))
            {
                descriptor.Flags |= PipeRenderFlags.MaskRuptured;
                if (descriptor.RuptureStartTimeSeconds <= 0f)
                    descriptor.RuptureStartTimeSeconds = math.max(0.001f, ResolvePipeShaderClockSeconds());
            }
            else
            {
                descriptor.RuptureStartTimeSeconds = 0f;
            }

            float flow01 = math.max(ResolvePipeNodeFlow(leftNodeId), ResolvePipeNodeFlow(rightNodeId));
            if (flow01 > 0.001f)
            {
                descriptor.Flags |= PipeRenderFlags.MaskHasFluidFlow;
                descriptor.FlowScalar = flow01;
            }
        }

        private float ResolvePipeNodeFlow(uint nodeId)
        {
            return _pipeNodeFlow01.TryGetValue(nodeId, out float flow01) ? flow01 : 0f;
        }

        private static float ResolvePipeShaderClockSeconds()
        {
            return Time.timeSinceLevelLoad;
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
            RefreshLateFrameTickRegistration();
        }

        private void RemoveLink(BatchState batch, long linkId)
        {
            if (batch == null)
                return;

            if (!batch.Registrations.Remove(linkId))
                return;

            batch.Dirty = true;
            RefreshLateFrameTickRegistration();
        }

        private void SetBatchColor(BatchState batch, Color color)
        {
            if (batch == null || ColorsMatch(batch.Color, color))
                return;

            batch.Color = color;
            batch.MaterialColorDirty = true;
            if (batch.Registrations.Count <= 0 && batch.InstanceCount <= 0)
                return;

            batch.Dirty = true;
            RefreshLateFrameTickRegistration();
        }

        private void ProcessBatch(BatchState batch)
        {
            if (batch == null)
                return;

            if (batch.Dirty)
                RefreshBatchGpuData(batch);

            RenderBatch(batch);
        }

        private void RefreshBatchGpuData(BatchState batch)
        {
            int linkCount = math.min(batch.Registrations.Count, MaxRenderedLinksPerBatch);
            batch.InstanceCount = 0;
            batch.Dirty = false;

            if (linkCount <= 0)
                return;

            if (!EnsureBatchCapacity(batch, linkCount))
                return;

            int writeIndex = 0;
            float3 minBounds = new float3(float.MaxValue, float.MaxValue, float.MaxValue);
            float3 maxBounds = new float3(float.MinValue, float.MinValue, float.MinValue);
            Dictionary<long, SplineDescriptor>.Enumerator enumerator = batch.Registrations.GetEnumerator();
            while (writeIndex < linkCount && enumerator.MoveNext())
            {
                SplineDescriptor descriptor = enumerator.Current.Value;
                LogisticsPipeBuilder.ResolveControlPoints(in descriptor, out float3 p0, out float3 p1, out float3 p2, out float3 p3);
                float radius = math.max(0.001f, descriptor.Radius);
                batch.Descriptors[writeIndex] = descriptor;
                batch.InstanceData[writeIndex] = new FlexiblePipeInstanceGpuData
                {
                    P0Radius = new float4(p0, radius),
                    P1Flags = new float4(p1, (float)descriptor.Flags),
                    P2 = new float4(p2, descriptor.RuptureStartTimeSeconds),
                    P3 = new float4(p3, IsPowerFlowPipeBatch(batch) ? 1f : descriptor.FlowScalar)
                };

                float3 padding = new float3(radius + 0.25f);
                minBounds = math.min(minBounds, math.min(math.min(p0, p1), math.min(p2, p3)) - padding);
                maxBounds = math.max(maxBounds, math.max(math.max(p0, p1), math.max(p2, p3)) + padding);
                writeIndex++;
            }

            batch.InstanceCount = writeIndex;
            GraphicsBufferUploadUtility.UploadNativeArray(batch.InstanceBuffer, batch.InstanceData, writeIndex);
            ApplyBatchMaterialState(batch);

            float3 center = (minBounds + maxBounds) * 0.5f;
            float3 size = math.max(maxBounds - minBounds, new float3(0.05f, 0.05f, 0.05f));
            batch.WorldBounds = new Bounds(
                new Vector3(center.x, center.y, center.z),
                new Vector3(size.x, size.y, size.z));
        }

        private static void ApplyBatchMaterialState(BatchState batch)
        {
            if (batch.Material == null)
                return;

            batch.Material.SetBuffer(s_FlexiblePipeInstancesId, batch.InstanceBuffer);
            if (!batch.MaterialColorDirty && ColorsMatch(batch.AppliedColor, batch.Color))
                return;

            ApplyMaterialColor(batch.Material, batch.Color);
            batch.AppliedColor = batch.Color;
            batch.MaterialColorDirty = false;
        }

        private void RefreshLateFrameTickRegistration()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
            {
                TryUnregisterLateFrameTickable();
                return;
            }

            if (HasRenderableBatchWork())
                TryRegisterLateFrameTickable();
            else
                TryUnregisterLateFrameTickable();
        }

        private bool HasRenderableBatchWork()
        {
            for (int batchIndex = 0; batchIndex < _batches.Length; batchIndex++)
            {
                BatchState batch = _batches[batchIndex];
                if (batch == null)
                    continue;

                if (batch.Dirty || batch.InstanceCount > 0 || batch.Registrations.Count > 0)
                    return true;
            }

            return false;
        }

        private static bool ColorsMatch(Color lhs, Color rhs)
        {
            return lhs.r == rhs.r
                && lhs.g == rhs.g
                && lhs.b == rhs.b
                && lhs.a == rhs.a;
        }

        private static bool IsPowerFlowPipeBatch(BatchState batch)
        {
            return batch != null && (byte)batch.Kind <= (byte)BatchKind.PipesLine;
        }

        private void RenderBatch(BatchState batch)
        {
            if (batch.InstanceCount <= 0 || batch.Mesh == null || batch.Material == null || batch.InstanceBuffer == null)
                return;

            RenderParams renderParams = new RenderParams(batch.Material)
            {
                worldBounds = batch.WorldBounds,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                layer = gameObject.layer
            };
            UnityEngine.Graphics.RenderMeshPrimitives(renderParams, batch.Mesh, 0, batch.InstanceCount);
        }

        private static Mesh ResolveStaticCylinderMesh()
        {
            if (s_staticCylinderMesh != null)
                return s_staticCylinderMesh;

            s_staticCylinderMesh = Resources.GetBuiltinResource<Mesh>(BuiltinCylinderMeshName);
            if (s_staticCylinderMesh != null)
                return s_staticCylinderMesh;

            GameObject primitive = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            primitive.SetActive(false);
            primitive.hideFlags = HideFlags.HideAndDontSave;
            MeshFilter meshFilter = primitive.GetComponent<MeshFilter>();
            s_staticCylinderMesh = meshFilter != null ? meshFilter.sharedMesh : null;
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(primitive);
            else
                UnityEngine.Object.DestroyImmediate(primitive);
            return s_staticCylinderMesh;
        }

        private static bool IsSharedStaticMesh(Mesh mesh)
        {
            return mesh != null && ReferenceEquals(mesh, s_staticCylinderMesh);
        }

        private void RebaseRegistrationDictionary(Dictionary<long, SplineDescriptor> registrations, float3 shiftOffset)
        {
            if (registrations == null || registrations.Count <= 0)
                return;

            _pipeRuptureUpdateScratch.Clear();
            Dictionary<long, SplineDescriptor>.Enumerator enumerator = registrations.GetEnumerator();
            while (enumerator.MoveNext())
                _pipeRuptureUpdateScratch.Add(enumerator.Current.Key);

            int keyCount = _pipeRuptureUpdateScratch.Count;
            for (int i = 0; i < keyCount; i++)
            {
                long linkId = _pipeRuptureUpdateScratch[i];
                if (!registrations.TryGetValue(linkId, out SplineDescriptor descriptor))
                    continue;

                descriptor.Start -= shiftOffset;
                descriptor.End -= shiftOffset;
                registrations[linkId] = descriptor;
            }

            _pipeRuptureUpdateScratch.Clear();
        }

        private void RebaseBatchForOriginShift(BatchState batch, float3 shiftOffset)
        {
            if (batch == null)
                return;

            if (batch.Registrations.Count <= 0 && batch.InstanceCount <= 0)
                return;

            RebaseRegistrationDictionary(batch.Registrations, shiftOffset);
            batch.Dirty = true;
        }

        private static bool EnsureBatchCapacity(BatchState batch, int linkCapacity)
        {
            int safeLinkCapacity = math.max(1, linkCapacity);
            EnsureArrayCapacity(ref batch.Descriptors, safeLinkCapacity);
            EnsureArrayCapacity(ref batch.InstanceData, safeLinkCapacity);
            EnsureInstanceBufferCapacity(batch, safeLinkCapacity);
            return batch.InstanceBuffer != null;
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

        private static void EnsureArrayCapacity(ref NativeArray<FlexiblePipeInstanceGpuData> array, int requiredLength)
        {
            if (array.IsCreated && array.Length >= requiredLength)
                return;

            if (array.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(array);
                array.Dispose();
            }

            array = new NativeArray<FlexiblePipeInstanceGpuData>(requiredLength, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            NativeMemorySentinel.RegisterNativeArray(array, nameof(ConnectionSplineBatchRenderer), nameof(BatchState.InstanceData), NativeAllocationLifetime.Session);
        }

        private static void EnsureInstanceBufferCapacity(BatchState batch, int requiredLength)
        {
            if (batch.InstanceBuffer != null && batch.InstanceBuffer.count >= requiredLength)
                return;

            ReleaseBuffer(ref batch.InstanceBuffer);
            batch.InstanceBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<FlexiblePipeInstanceGpuData>(requiredLength);
        }

        private static void DisposeBatch(BatchState batch)
        {
            if (batch == null)
                return;

            DisposeNativeArray(ref batch.Descriptors);
            DisposeNativeArray(ref batch.InstanceData);
            ReleaseBuffer(ref batch.InstanceBuffer);
            batch.Registrations.Clear();
            batch.InstanceCount = 0;
            batch.Dirty = false;

            if (batch.Mesh != null && !IsSharedStaticMesh(batch.Mesh))
                Destroy(batch.Mesh);
            batch.Mesh = null;

            if (batch.Material != null)
                Destroy(batch.Material);
            batch.Material = null;
        }

        private static void DisposeNativeArray<T>(ref NativeArray<T> array)
            where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(array);
            array.Dispose();
            array = default;
        }

        private static void ReleaseBuffer(ref GraphicsBuffer buffer)
        {
            if (buffer == null)
                return;

            buffer.Release();
            buffer = null;
        }
    }
}
