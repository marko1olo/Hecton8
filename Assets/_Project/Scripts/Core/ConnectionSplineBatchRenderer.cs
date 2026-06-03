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
    public sealed class ConnectionSplineBatchRenderer : MonoBehaviour, IConnectionSplineBatchRendererService, IServiceHeartbeat, IServiceShutdown, ISlowTickable, ILateFrameTickable, IOriginShiftListener, IGlobalRegistryHotSwapListener
    {
        private const int DefaultBatchCapacity = 100;
        private const int MaxRenderedLinksPerBatch = 64;
        private const float FarPipeSpanThresholdMetersSq = 40f * 40f;
        private const float RelayRadiusMeters = 0.028f;

        private static readonly int s_FlexiblePipeInstancesId = Shader.PropertyToID("_HectonFlexiblePipeInstances");
        private static readonly int s_BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int s_ColorId = Shader.PropertyToID("_Color");
        private static readonly int s_SmoothnessId = Shader.PropertyToID("_Smoothness");
        private static readonly int s_MetallicId = Shader.PropertyToID("_Metallic");
        private static readonly int s_LogisticsPathHighlightId = Shader.PropertyToID("_HectonLogisticsPathHighlight");
        private static IConnectionSplineBatchRendererService s_activeService;
        private static ConnectionSplineBatchRenderer s_activeRuntimeInstance;
        private static bool s_pendingLogisticsPathHighlightActive;
        private static bool s_logisticsPathHighlightDirty;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticPresentationState()
        {
            ShutdownActiveRuntimeForEditorReload();
            s_activeRuntimeInstance = null;
            s_activeService = null;
            s_pendingLogisticsPathHighlightActive = false;
            s_logisticsPathHighlightDirty = false;
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void RegisterEditorReloadHooks()
        {
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= ShutdownActiveRuntimeForEditorReload;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += ShutdownActiveRuntimeForEditorReload;
            UnityEditor.EditorApplication.quitting -= ShutdownActiveRuntimeForEditorReload;
            UnityEditor.EditorApplication.quitting += ShutdownActiveRuntimeForEditorReload;
            UnityEditor.EditorApplication.playModeStateChanged -= HandleEditorPlayModeStateChanged;
            UnityEditor.EditorApplication.playModeStateChanged += HandleEditorPlayModeStateChanged;
        }

        private static void HandleEditorPlayModeStateChanged(UnityEditor.PlayModeStateChange state)
        {
            if (state == UnityEditor.PlayModeStateChange.ExitingEditMode ||
                state == UnityEditor.PlayModeStateChange.ExitingPlayMode ||
                state == UnityEditor.PlayModeStateChange.EnteredEditMode)
            {
                ShutdownActiveRuntimeForEditorReload();
            }
        }
#endif

        private static void ShutdownActiveRuntimeForEditorReload()
        {
            ConnectionSplineBatchRenderer runtime = s_activeRuntimeInstance;
            if (runtime != null)
                runtime.ShutdownServiceState();
        }

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
            internal MaterialPropertyBlock MaterialProperties;
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
        private bool _registeredSlowTick;
        private bool _lateFrameTickDormant;
        private bool _dispatcherAvailable;
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

        [Header("Authored Pipe Rendering")]
        [SerializeField, Tooltip("Required authored cylinder-like pipe segment mesh. Runtime primitive fallback is forbidden.")]
        private Mesh pipeSegmentMesh;
        [SerializeField, Tooltip("Required authored flexible pipe material. Per-batch color and instance buffers are supplied through MaterialPropertyBlock.")]
        private Material pipeBatchMaterial;

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
            if (s_logisticsPathHighlightDirty && s_pendingLogisticsPathHighlightActive == active)
                return;

            s_pendingLogisticsPathHighlightActive = active;
            s_logisticsPathHighlightDirty = true;
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

            s_activeRuntimeInstance = this;

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
            _dispatcherAvailable = GlobalRegistry.Dispatcher != null;
            EnsureRuntimeRegistrations();
        }

        private void OnEnable()
        {
            EnsureRuntimeRegistrations();
        }

        private void OnDisable()
        {
            TryUnregisterOriginShiftListener();
            TryUnregisterSlowTickable();
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

            _dispatcherAvailable = currentService != null;
            TryUnregisterSlowTickable();
            TryUnregisterLateFrameTickable();
            if (_dispatcherAvailable && isActiveAndEnabled)
                EnsureRuntimeRegistrations();
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
            TryRegisterSlowTickable();
            RefreshLateFrameTickRegistration();
        }

        private void TryRegisterSlowTickable()
        {
            if (_registeredSlowTick || !Application.isPlaying || !_dispatcherAvailable || GlobalRegistry.Dispatcher == null)
                return;

            _registeredSlowTick = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregisterSlowTickable()
        {
            if (!_registeredSlowTick)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
            _registeredSlowTick = false;
        }

        private void TryRegisterLateFrameTickable()
        {
            if (_registeredLateFrameTick || !_dispatcherAvailable || GlobalRegistry.Dispatcher == null)
                return;

            _registeredLateFrameTick = SystemDispatcher.Register((ILateFrameTickable)this, PriorityLayer.Environment);
        }

        private void TryUnregisterLateFrameTickable()
        {
            if (!_registeredLateFrameTick)
                return;

            SystemDispatcher.Unregister((ILateFrameTickable)this, PriorityLayer.Environment);
            _registeredLateFrameTick = false;
            _lateFrameTickDormant = false;
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
            if (_lateFrameTickDormant && !s_logisticsPathHighlightDirty && !HasRenderableBatchWork())
                return;

            _lateFrameTickDormant = false;

            for (int batchIndex = 0; batchIndex < _batches.Length; batchIndex++)
                ProcessBatch(_batches[batchIndex]);

            if (!s_logisticsPathHighlightDirty && !HasRenderableBatchWork())
                _lateFrameTickDormant = true;
        }

        public void SlowTick()
        {
            for (int batchIndex = 0; batchIndex < _batches.Length; batchIndex++)
            {
                BatchState batch = _batches[batchIndex];
                if (batch == null)
                    continue;

                int linkCount = math.min(batch.Registrations.Count, MaxRenderedLinksPerBatch);
                if (linkCount <= 0)
                    continue;

                if (HasBatchCapacity(batch, linkCount))
                    continue;

                batch.Dirty = false;
            }
        }

        internal static void FlushVisualSyncShaderState()
        {
            if (!s_logisticsPathHighlightDirty)
                return;

            s_logisticsPathHighlightDirty = false;
            Shader.SetGlobalFloat(s_LogisticsPathHighlightId, s_pendingLogisticsPathHighlightActive ? 1f : 0f);
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
            TryUnregisterSlowTickable();
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

            if (ReferenceEquals(s_activeRuntimeInstance, this))
                s_activeRuntimeInstance = null;

            _dispatcherAvailable = false;
            _serviceRegistered = false;
            _shutdownComplete = true;
        }

        private void InitializeBatch(int index, BatchKind kind, Color color, float radius)
        {
            BatchState batch = new BatchState
            {
                Kind = kind,
                Color = color,
                AppliedColor = default,
                Radius = radius,
                Mesh = ResolveAuthoredPipeMeshCold(),
                Material = ResolveAuthoredPipeMaterialCold(),
                Dirty = false,
                MaterialColorDirty = true
            };

            EnsureBatchCapacityCold(batch, MaxRenderedLinksPerBatch);
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

        private Mesh ResolveAuthoredPipeMeshCold()
        {
            return pipeSegmentMesh;
        }

        private Material ResolveAuthoredPipeMaterialCold()
        {
            return pipeBatchMaterial;
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
            return (float)SystemDispatcher.CurrentUnscaledTimeSeconds;
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

            if (!HasBatchCapacity(batch, linkCount))
            {
                batch.Dirty = true;
                return;
            }

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
            if (batch.Material == null || batch.MaterialProperties == null)
                return;

            batch.MaterialProperties.SetBuffer(s_FlexiblePipeInstancesId, batch.InstanceBuffer);
            if (!batch.MaterialColorDirty && ColorsMatch(batch.AppliedColor, batch.Color))
                return;

            batch.MaterialProperties.SetColor(s_BaseColorId, batch.Color);
            batch.MaterialProperties.SetColor(s_ColorId, batch.Color);
            batch.MaterialProperties.SetFloat(s_SmoothnessId, 0.22f);
            batch.MaterialProperties.SetFloat(s_MetallicId, 0f);
            batch.AppliedColor = batch.Color;
            batch.MaterialColorDirty = false;
        }

        private void RefreshLateFrameTickRegistration()
        {
            if (!Application.isPlaying || !_dispatcherAvailable)
            {
                TryUnregisterLateFrameTickable();
                return;
            }

            if (HasRenderableBatchWork())
            {
                _lateFrameTickDormant = false;
                TryRegisterLateFrameTickable();
                return;
            }

            if (_registeredLateFrameTick)
                _lateFrameTickDormant = true;
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
                layer = gameObject.layer,
                matProps = batch.MaterialProperties
            };
            UnityEngine.Graphics.RenderMeshPrimitives(renderParams, batch.Mesh, 0, batch.InstanceCount);
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

        private static bool HasBatchCapacity(BatchState batch, int linkCapacity)
        {
            int safeLinkCapacity = math.max(1, linkCapacity);
            return batch != null &&
                   batch.Descriptors.IsCreated &&
                   batch.Descriptors.Length >= safeLinkCapacity &&
                   batch.InstanceData.IsCreated &&
                   batch.InstanceData.Length >= safeLinkCapacity &&
                   batch.InstanceBuffer != null &&
                   batch.InstanceBuffer.IsValid() &&
                   batch.InstanceBuffer.count >= safeLinkCapacity;
        }

        private static bool EnsureBatchCapacityCold(BatchState batch, int linkCapacity)
        {
            int safeLinkCapacity = math.max(1, linkCapacity);
            EnsureBatchMaterialPropertiesCold(batch);
            EnsureArrayCapacityCold(ref batch.Descriptors, safeLinkCapacity);
            EnsureArrayCapacityCold(ref batch.InstanceData, safeLinkCapacity);
            EnsureInstanceBufferCapacityCold(batch, safeLinkCapacity);
            return HasBatchCapacity(batch, safeLinkCapacity);
        }

        private static void EnsureBatchMaterialPropertiesCold(BatchState batch)
        {
            if (batch.MaterialProperties != null)
                return;

            batch.MaterialProperties = new MaterialPropertyBlock();
        }

        private static void EnsureArrayCapacityCold(ref NativeArray<SplineDescriptor> array, int requiredLength)
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

        private static void EnsureArrayCapacityCold(ref NativeArray<FlexiblePipeInstanceGpuData> array, int requiredLength)
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

        private static void EnsureInstanceBufferCapacityCold(BatchState batch, int requiredLength)
        {
            if (batch.InstanceBuffer != null && batch.InstanceBuffer.IsValid() && batch.InstanceBuffer.count >= requiredLength)
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

            batch.Mesh = null;

            if (batch.MaterialProperties != null)
                batch.MaterialProperties.Clear();
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
