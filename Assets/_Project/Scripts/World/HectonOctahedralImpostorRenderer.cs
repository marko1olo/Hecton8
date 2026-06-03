using Hecton8.Core;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.World
{
    /// <summary>
    /// Renders far-field octahedral impostors through a single RenderMeshIndirect draw.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-88)]
    public sealed class HectonOctahedralImpostorRenderer : MonoBehaviour, ILateFrameTickable, IOriginShiftListener, IStreamingHlodMatrixRenderer, IGlobalRegistryHotSwapListener
    {
        private const int TelemetryIntervalFrames = 60;
        private const uint TelemetryHash = 0x4F435449u; // "OCTI"
        private const float ImpostorFadeSecondsRcp = 0.6666667f;

        private static readonly int ImpostorInstancesId = Shader.PropertyToID("_HectonImpostorInstances");
        private static readonly int VisibleInstancesId = Shader.PropertyToID("_HectonVisibleInstances");
        private static readonly int UseVisibleMatrixStreamId = Shader.PropertyToID("_HectonUseVisibleMatrixStream");
        private static readonly int ImpostorTimeSecondsId = Shader.PropertyToID("_HectonImpostorTimeSeconds");
        private static readonly int ImpostorFadeOutSecondsId = Shader.PropertyToID("_HectonImpostorFadeOutSeconds");
        private static readonly int GlobalFloatingOffsetId = Shader.PropertyToID("_GlobalFloatingOffset");
        private static readonly int AlbedoDepthAtlasId = Shader.PropertyToID("_ImpostorAlbedoDepthAtlas");
        private static readonly int NormalDepthAtlasId = Shader.PropertyToID("_ImpostorNormalDepthAtlas");
        private static readonly int AtlasGridId = Shader.PropertyToID("_HectonImpostorAtlasGrid");
        private static readonly int DepthScaleMetersId = Shader.PropertyToID("_HectonImpostorDepthScaleMeters");
        private static readonly int GlobalQualityWeightId = Shader.PropertyToID("_HectonGlobalQualityWeight");

        [Header("-- Rendering ----------------")]
        [SerializeField] private Mesh _quadMesh;
        [SerializeField] private Material _material;
        [SerializeField] private HectonOctahedralImpostorData _impostorData;
        [SerializeField, Min(0)] private int _subMeshIndex;
        [SerializeField] private Camera _cameraOverride;

        [Header("-- Bounds -------------------")]
        [SerializeField] private Vector3 _fallbackBoundsCenterOffset = Vector3.zero;
        [SerializeField] private Vector3 _fallbackBoundsSize = new Vector3(3000f, 1600f, 3000f);

        [Header("-- Diagnostics --------------")]
        [SerializeField] private int _debugBoundInstanceCount;

        private GraphicsBuffer _instanceBufferA;
        private GraphicsBuffer _instanceBufferB;
        private GraphicsBuffer _activeInstanceBuffer;
        private GraphicsBuffer _matrixSourceBufferA;
        private GraphicsBuffer _matrixSourceBufferB;
        private GraphicsBuffer _activeMatrixSourceBuffer;
        private GraphicsBuffer _argsBuffer;
        private Mesh _argsMesh;
        private Bounds _drawBounds;
        private int _instanceCount;
        private int _lastArgsInstanceCount = -1;
        private int _lastQualityMilli = int.MinValue;
        private int _lastTelemetryTick = -TelemetryIntervalFrames;
        private int _telemetryTickCounter;
        private int _matrixSourceCapacity;
        private int _matrixSourceUploadedCount;
        private int _instanceUploadBufferIndex;
        private int _matrixSourceUploadBufferIndex;
        private float _lastBoundsRadius = 1f;
        private float _impostorTimeSeconds;
        private bool _useVisibleMatrixStream;
        private bool _hasBoundsOverride;
        private bool _registeredTick;
        private IInstanceCullingService _instanceCullingService;
        private bool _hotSwapListenerRegistered;

        public int BoundInstanceCount => _instanceCount;

        /// <summary>
        /// True when the renderer is consuming the compute-culling visible matrix stream.
        /// </summary>
        public bool IsUsingVisibleMatrixStream => _useVisibleMatrixStream;

        private void Awake()
        {
            _drawBounds = new Bounds(transform.position + _fallbackBoundsCenterOffset, _fallbackBoundsSize);
        }

        private void OnEnable()
        {
            HectonFloatingOrigin.RegisterListener(this);
            CacheInstanceCullingServiceCold();
            EnsureIndirectArgsBufferCold(ResolveQuadMesh());
            TryRegisterHotSwapListener();
            RegisterTick();
        }

        private void OnDisable()
        {
            HectonFloatingOrigin.UnregisterListener(this);
            UnregisterTick();
            TryUnregisterHotSwapListener();
        }

        private void OnDestroy()
        {
            HectonFloatingOrigin.UnregisterListener(this);
            UnregisterTick();
            TryUnregisterHotSwapListener();
            ReleaseResources();
        }

        public void LateFrameTick()
        {
            float deltaTime = SystemDispatcher.CurrentFrameDeltaTime;
            _telemetryTickCounter++;
            if (deltaTime > 0f && math.isfinite(deltaTime))
                _impostorTimeSeconds += math.min(deltaTime, 0.25f);

            if (_instanceCount <= 0)
                return;

            Mesh mesh = ResolveQuadMesh();
            Material material = ResolveMaterial();
            if (mesh == null || material == null)
                return;

            bool useMatrixStream = _useVisibleMatrixStream &&
                                   _instanceCullingService != null &&
                                   _instanceCullingService.VisibleInstancesBuffer != null &&
                                   _instanceCullingService.IndirectArgsBuffer != null;
            if (!useMatrixStream && _activeInstanceBuffer == null)
                return;

            if (!useMatrixStream && !HasIndirectArgsBufferReady(mesh))
                return;

            GraphicsBuffer drawInstanceBuffer = useMatrixStream
                ? _instanceCullingService.VisibleInstancesBuffer
                : _activeInstanceBuffer;
            if (!TryBindDrawMaterial(material, useMatrixStream, drawInstanceBuffer))
                return;

            RenderParams renderParams = new RenderParams(material)
            {
                worldBounds = ResolveDrawBounds(),
                layer = gameObject.layer,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                camera = _cameraOverride
            };
            GraphicsBuffer argsBuffer = useMatrixStream ? _instanceCullingService.IndirectArgsBuffer : _argsBuffer;
            UnityEngine.Graphics.RenderMeshIndirect(renderParams, mesh, argsBuffer, 1, 0);
            ReportTelemetryIfDue();
        }

        public void BindNativeInstances(NativeArray<OctahedralImpostorInstance> instances, int instanceCount)
        {
            if (!instances.IsCreated || instanceCount <= 0 || instances.Length < instanceCount)
            {
                ClearBinding();
                return;
            }

            EnsureInstanceBufferCapacity(instanceCount);
            GraphicsBuffer instanceWriteBuffer = ResolveInstanceWriteBuffer();
            if (instanceWriteBuffer == null || !instanceWriteBuffer.IsValid())
            {
                ClearBinding();
                return;
            }

            Bounds combinedBounds = default;
            bool hasCombinedBounds = false;
            Vector3 floatingOffset = ResolveGlobalFloatingOffset();
            for (int i = 0; i < instanceCount; i++)
            {
                OctahedralImpostorInstance instance = instances[i];

                Bounds runtimeBounds = instance.ToUniverseBounds();
                runtimeBounds.center += floatingOffset;
                if (hasCombinedBounds)
                    combinedBounds.Encapsulate(runtimeBounds);
                else
                {
                    combinedBounds = runtimeBounds;
                    hasCombinedBounds = true;
                }
            }

            GraphicsBufferUploadUtility.UploadNativeArray(instanceWriteBuffer, instances, instanceCount);
            _activeInstanceBuffer = instanceWriteBuffer;
            _instanceUploadBufferIndex ^= 1;
            _instanceCount = instanceCount;
            _debugBoundInstanceCount = instanceCount;
            _drawBounds = hasCombinedBounds ? combinedBounds : new Bounds(transform.position + _fallbackBoundsCenterOffset, _fallbackBoundsSize);
            _hasBoundsOverride = hasCombinedBounds;
            _lastArgsInstanceCount = -1;
            EnsureIndirectArgsBufferCold(ResolveQuadMesh());
        }

        public void BindNativeMatrices(NativeArray<float4x4> matrices, int instanceCount, float boundsRadius)
        {
            BindNativeMatrices(matrices, instanceCount, boundsRadius, forceUpload: true);
        }

        public void BindNativeMatrices(NativeArray<float4x4> matrices, int instanceCount, float boundsRadius, bool forceUpload)
        {
            if (!matrices.IsCreated || instanceCount <= 0 || matrices.Length < instanceCount)
            {
                ClearBinding();
                return;
            }

            EnsureMatrixSourceCapacity(instanceCount);
            GraphicsBuffer matrixSourceWriteBuffer = ResolveMatrixSourceWriteBuffer();
            if (matrixSourceWriteBuffer == null || !matrixSourceWriteBuffer.IsValid())
            {
                BindMatricesAsOctahedralFallback(matrices, instanceCount);
                return;
            }

            bool needsUpload = forceUpload || _matrixSourceUploadedCount != instanceCount;
            if (needsUpload)
            {
                GraphicsBufferUploadUtility.UploadNativeArray(matrixSourceWriteBuffer, matrices, instanceCount);
                _activeMatrixSourceBuffer = matrixSourceWriteBuffer;
                _matrixSourceUploadBufferIndex ^= 1;
                _matrixSourceUploadedCount = instanceCount;
            }

            _instanceCount = instanceCount;
            _debugBoundInstanceCount = instanceCount;
            _lastBoundsRadius = Mathf.Max(0.5f, boundsRadius);
            if (needsUpload || !_hasBoundsOverride)
                ResolveMatrixBounds(matrices, instanceCount);

            bool wasUsingVisibleMatrixStream = _useVisibleMatrixStream;
            IInstanceCullingService culling = ResolveInstanceCullingService();
            Mesh mesh = ResolveQuadMesh();
            if (culling == null || !culling.IsAvailable || mesh == null)
            {
                _useVisibleMatrixStream = false;
                if (needsUpload || wasUsingVisibleMatrixStream || _activeInstanceBuffer == null || _instanceCount != instanceCount)
                    BindMatricesAsOctahedralFallback(matrices, instanceCount);
                else
                    EnsureIndirectArgsBufferCold(mesh);
                return;
            }

            int safeSubMesh = Mathf.Clamp(_subMeshIndex, 0, Mathf.Max(0, mesh.subMeshCount - 1));
            float globalQualityWeight = ResolveGlobalQualityWeight01();
            InstanceCullingDispatchDescriptor descriptor = new InstanceCullingDispatchDescriptor
            {
                AllInstancesBuffer = _activeMatrixSourceBuffer,
                InstanceCount = instanceCount,
                BoundsRadius = _lastBoundsRadius,
                MaxCullDistanceMeters = Mathf.Max(1000f, _lastBoundsRadius * 64f),
                VramUsedMb = VRAMBudgetTracker.EstimatedVRAMBytes * GlobalTelemetryBus.BytesToMegabytes,
                GlobalQualityWeight = globalQualityWeight,
                Flags = InstanceCullingDispatchFlags.None,
                IndirectArgs = new InstanceCullingIndirectArgs
                {
                    IndexCountPerInstance = mesh.GetIndexCount(safeSubMesh),
                    StartIndex = mesh.GetIndexStart(safeSubMesh),
                    BaseVertexIndex = unchecked((uint)Mathf.Max(0, mesh.GetBaseVertex(safeSubMesh))),
                    StartInstance = 0u
                }
            };

            _useVisibleMatrixStream = culling.Dispatch(in descriptor);
            if (!_useVisibleMatrixStream)
            {
                if (needsUpload || wasUsingVisibleMatrixStream || _activeInstanceBuffer == null || _instanceCount != instanceCount)
                    BindMatricesAsOctahedralFallback(matrices, instanceCount);
                else
                    EnsureIndirectArgsBufferCold(mesh);
            }
        }

        public void BindNativeHLOD(NativeArray<HLODData> hlodEntries, int hlodCount)
        {
            if (!hlodEntries.IsCreated || hlodCount <= 0 || hlodEntries.Length < hlodCount)
            {
                ClearBinding();
                return;
            }

            EnsureInstanceBufferCapacity(hlodCount);
            GraphicsBuffer instanceWriteBuffer = ResolveInstanceWriteBuffer();
            if (instanceWriteBuffer == null || !instanceWriteBuffer.IsValid())
            {
                ClearBinding();
                return;
            }

            Bounds combinedBounds = default;
            bool hasCombinedBounds = false;
            Vector3 floatingOffset = ResolveGlobalFloatingOffset();
            long uploadBytes = GraphicsBufferUploadUtility.EstimateUploadBytes<OctahedralImpostorInstance>(hlodCount);
            if (!GraphicsBufferUploadUtility.TryBeginManualUpload(uploadBytes))
                return;

            bool bufferLocked = false;
            bool uploadAccepted = false;
            bool unlockSucceeded = false;
            NativeArray<OctahedralImpostorInstance> upload = default;
            try
            {
                upload = instanceWriteBuffer.LockBufferForWrite<OctahedralImpostorInstance>(0, hlodCount);
                bufferLocked = true;
                for (int i = 0; i < hlodCount; i++)
                {
                    HLODData entry = hlodEntries[i];
                    Vector3 size = new Vector3(
                        Mathf.Max(0.5f, entry.Size.x),
                        Mathf.Max(0.5f, entry.Size.y),
                        Mathf.Max(0.5f, entry.Size.z));
                    OctahedralImpostorInstance instance = OctahedralImpostorInstance.Create(
                        entry.Center,
                        size,
                        entry.Fade01,
                        0f,
                        HectonChunkImpostorResidency.FlagUseImpostor);
                    upload[i] = instance;

                    Bounds runtimeBounds = instance.ToUniverseBounds();
                    runtimeBounds.center += floatingOffset;
                    if (hasCombinedBounds)
                        combinedBounds.Encapsulate(runtimeBounds);
                    else
                    {
                        combinedBounds = runtimeBounds;
                        hasCombinedBounds = true;
                    }
                }

                uploadAccepted = true;
            }
            finally
            {
                try
                {
                    if (bufferLocked)
                    {
                        instanceWriteBuffer.UnlockBufferAfterWrite<OctahedralImpostorInstance>(hlodCount);
                        unlockSucceeded = true;
                    }
                }
                finally
                {
                    if (uploadAccepted && unlockSucceeded)
                        GraphicsBufferUploadUtility.CompleteManualUpload(uploadBytes);
                    else
                        GraphicsBufferUploadUtility.CancelManualUpload(uploadBytes);
                }
            }

            _activeInstanceBuffer = instanceWriteBuffer;
            _instanceUploadBufferIndex ^= 1;
            _instanceCount = hlodCount;
            _debugBoundInstanceCount = hlodCount;
            _drawBounds = hasCombinedBounds ? combinedBounds : new Bounds(transform.position + _fallbackBoundsCenterOffset, _fallbackBoundsSize);
            _hasBoundsOverride = hasCombinedBounds;
            _lastArgsInstanceCount = -1;
            EnsureIndirectArgsBufferCold(ResolveQuadMesh());
        }

        private void BindMatricesAsOctahedralFallback(NativeArray<float4x4> matrices, int instanceCount)
        {
            EnsureInstanceBufferCapacity(instanceCount);
            GraphicsBuffer instanceWriteBuffer = ResolveInstanceWriteBuffer();
            if (instanceWriteBuffer == null || !instanceWriteBuffer.IsValid())
            {
                ClearBinding();
                return;
            }

            Bounds combinedBounds = default;
            bool hasCombinedBounds = false;
            Vector3 floatingOffset = ResolveGlobalFloatingOffset();
            long uploadBytes = GraphicsBufferUploadUtility.EstimateUploadBytes<OctahedralImpostorInstance>(instanceCount);
            if (!GraphicsBufferUploadUtility.TryBeginManualUpload(uploadBytes))
                return;

            bool bufferLocked = false;
            bool uploadAccepted = false;
            bool unlockSucceeded = false;
            NativeArray<OctahedralImpostorInstance> upload = default;
            try
            {
                upload = instanceWriteBuffer.LockBufferForWrite<OctahedralImpostorInstance>(0, instanceCount);
                bufferLocked = true;
                for (int i = 0; i < instanceCount; i++)
                {
                    float4x4 matrix = matrices[i];
                    Vector3 center = new Vector3(matrix.c3.x, matrix.c3.y, matrix.c3.z);
                    Vector3 size = new Vector3(
                        Mathf.Max(0.5f, math.abs(matrix.c0.x)),
                        Mathf.Max(0.5f, math.abs(matrix.c1.y)),
                        Mathf.Max(0.5f, math.abs(matrix.c2.z)));
                    float age01 = math.saturate((_impostorTimeSeconds - matrix.c3.w) * ImpostorFadeSecondsRcp);
                    float fadeAge = matrix.c0.w < 0f ? 1f - age01 : age01;
                    uint flags = math.asuint(matrix.c2.w);
                    upload[i] = OctahedralImpostorInstance.Create(
                        center,
                        size,
                        fadeAge,
                        0f,
                        flags == 0u ? HectonChunkImpostorResidency.FlagUseImpostor : flags);

                    Bounds bounds = new Bounds(center + floatingOffset, size);
                    if (hasCombinedBounds)
                        combinedBounds.Encapsulate(bounds);
                    else
                    {
                        combinedBounds = bounds;
                        hasCombinedBounds = true;
                    }
                }

                uploadAccepted = true;
            }
            finally
            {
                try
                {
                    if (bufferLocked)
                    {
                        instanceWriteBuffer.UnlockBufferAfterWrite<OctahedralImpostorInstance>(instanceCount);
                        unlockSucceeded = true;
                    }
                }
                finally
                {
                    if (uploadAccepted && unlockSucceeded)
                        GraphicsBufferUploadUtility.CompleteManualUpload(uploadBytes);
                    else
                        GraphicsBufferUploadUtility.CancelManualUpload(uploadBytes);
                }
            }

            _activeInstanceBuffer = instanceWriteBuffer;
            _instanceUploadBufferIndex ^= 1;
            _useVisibleMatrixStream = false;
            _instanceCount = instanceCount;
            _debugBoundInstanceCount = instanceCount;
            _drawBounds = hasCombinedBounds ? combinedBounds : new Bounds(transform.position + _fallbackBoundsCenterOffset, _fallbackBoundsSize);
            _hasBoundsOverride = hasCombinedBounds;
            _lastArgsInstanceCount = -1;
            EnsureIndirectArgsBufferCold(ResolveQuadMesh());
        }

        private void ResolveMatrixBounds(NativeArray<float4x4> matrices, int instanceCount)
        {
            Bounds combinedBounds = default;
            bool hasCombinedBounds = false;
            Vector3 floatingOffset = ResolveGlobalFloatingOffset();
            for (int i = 0; i < instanceCount; i++)
            {
                float4x4 matrix = matrices[i];
                Vector3 center = new Vector3(matrix.c3.x, matrix.c3.y, matrix.c3.z) + floatingOffset;
                Vector3 size = new Vector3(
                    Mathf.Max(0.5f, math.abs(matrix.c0.x)),
                    Mathf.Max(0.5f, math.abs(matrix.c1.y)),
                    Mathf.Max(0.5f, math.abs(matrix.c2.z)));
                Bounds bounds = new Bounds(center, size);
                if (hasCombinedBounds)
                    combinedBounds.Encapsulate(bounds);
                else
                {
                    combinedBounds = bounds;
                    hasCombinedBounds = true;
                }
            }

            _drawBounds = hasCombinedBounds ? combinedBounds : new Bounds(transform.position + _fallbackBoundsCenterOffset, _fallbackBoundsSize);
            _hasBoundsOverride = hasCombinedBounds;
        }

        private void EnsureMatrixSourceCapacity(int instanceCount)
        {
            int nextCapacity = Mathf.NextPowerOfTwo(Mathf.Max(1, instanceCount));
            if (_matrixSourceBufferA != null &&
                _matrixSourceBufferA.IsValid() &&
                _matrixSourceBufferB != null &&
                _matrixSourceBufferB.IsValid() &&
                _matrixSourceCapacity >= nextCapacity)
            {
                if (_activeMatrixSourceBuffer == null)
                    _activeMatrixSourceBuffer = _matrixSourceBufferA;
                return;
            }

            ReleaseGraphicsBuffer(ref _matrixSourceBufferA);
            ReleaseGraphicsBuffer(ref _matrixSourceBufferB);

            _matrixSourceBufferA = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float4x4>(nextCapacity); // COLD ALLOC: GraphicsBuffer[NextPowerOfTwo(requiredCount)] A - HLOD matrix source for instance culling - owner: HectonOctahedralImpostorRenderer
            _matrixSourceBufferB = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float4x4>(nextCapacity); // COLD ALLOC: GraphicsBuffer[NextPowerOfTwo(requiredCount)] B - HLOD matrix source for instance culling - owner: HectonOctahedralImpostorRenderer
            _activeMatrixSourceBuffer = _matrixSourceBufferA;
            _matrixSourceUploadBufferIndex = 0;
            _matrixSourceCapacity = nextCapacity;
            _matrixSourceUploadedCount = 0;
        }

        private GraphicsBuffer ResolveMatrixSourceWriteBuffer()
        {
            GraphicsBuffer writeBuffer = _matrixSourceUploadBufferIndex == 0
                ? _matrixSourceBufferA
                : _matrixSourceBufferB;
            if (writeBuffer != null && writeBuffer.IsValid())
                return writeBuffer;

            GraphicsBuffer fallback = ReferenceEquals(_activeMatrixSourceBuffer, _matrixSourceBufferA)
                ? _matrixSourceBufferB
                : _matrixSourceBufferA;
            return fallback != null && fallback.IsValid() ? fallback : null;
        }

        private IInstanceCullingService ResolveInstanceCullingService()
        {
            return _instanceCullingService;
        }

        private void CacheInstanceCullingServiceCold()
        {
            _instanceCullingService = GlobalRegistry.InstanceCulling;
        }

        public void ClearBinding()
        {
            _instanceCount = 0;
            _debugBoundInstanceCount = 0;
            _hasBoundsOverride = false;
            _lastArgsInstanceCount = -1;
            _useVisibleMatrixStream = false;
            _matrixSourceUploadedCount = 0;
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            if (!isActiveAndEnabled || !_hasBoundsOverride || shiftData.ShiftOffset.sqrMagnitude <= 0.0001f)
                return;

            Bounds drawBounds = _drawBounds;
            drawBounds.center -= shiftData.ShiftOffset;
            _drawBounds = drawBounds;
        }

        private void RegisterTick()
        {
            if (_registeredTick || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredTick = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void UnregisterTick()
        {
            if (!_registeredTick)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
            _registeredTick = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.InstanceCullingRuntime)
            {
                _instanceCullingService = currentService as IInstanceCullingService;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher && currentService != null && isActiveAndEnabled)
            {
                _registeredTick = false;
                RegisterTick();
            }
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapListenerRegistered || !Application.isPlaying)
                return;

            _hotSwapListenerRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapListenerRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapListenerRegistered = false;
        }

        private void EnsureInstanceBufferCapacity(int instanceCount)
        {
            int nextCapacity = Mathf.NextPowerOfTwo(Mathf.Max(1, instanceCount));
            if (_instanceBufferA != null &&
                _instanceBufferA.IsValid() &&
                _instanceBufferA.count >= nextCapacity &&
                _instanceBufferB != null &&
                _instanceBufferB.IsValid() &&
                _instanceBufferB.count >= nextCapacity)
            {
                if (_activeInstanceBuffer == null)
                    _activeInstanceBuffer = _instanceBufferA;
                return;
            }

            ReleaseGraphicsBuffer(ref _instanceBufferA);
            ReleaseGraphicsBuffer(ref _instanceBufferB);

            _instanceBufferA = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<OctahedralImpostorInstance>(nextCapacity); // COLD ALLOC: GraphicsBuffer[NextPowerOfTwo(requiredCount)] A - impostor instance buffer - owner: HectonOctahedralImpostorRenderer
            _instanceBufferB = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<OctahedralImpostorInstance>(nextCapacity); // COLD ALLOC: GraphicsBuffer[NextPowerOfTwo(requiredCount)] B - impostor instance buffer - owner: HectonOctahedralImpostorRenderer
            _activeInstanceBuffer = _instanceBufferA;
            _instanceUploadBufferIndex = 0;
        }

        private GraphicsBuffer ResolveInstanceWriteBuffer()
        {
            GraphicsBuffer writeBuffer = _instanceUploadBufferIndex == 0 ? _instanceBufferA : _instanceBufferB;
            if (writeBuffer != null && writeBuffer.IsValid())
                return writeBuffer;

            GraphicsBuffer fallback = ReferenceEquals(_activeInstanceBuffer, _instanceBufferA)
                ? _instanceBufferB
                : _instanceBufferA;
            return fallback != null && fallback.IsValid() ? fallback : null;
        }

        private bool HasIndirectArgsBufferReady(Mesh mesh)
        {
            return _argsBuffer != null &&
                   _argsBuffer.IsValid() &&
                   mesh != null &&
                   ReferenceEquals(_argsMesh, mesh) &&
                   _lastArgsInstanceCount == _instanceCount;
        }

        private void EnsureIndirectArgsBufferCold(Mesh mesh)
        {
            if (_argsBuffer == null)
            {
                _argsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, GraphicsBuffer.UsageFlags.LockBufferForWrite, 1, GraphicsBuffer.IndirectDrawIndexedArgs.size); // COLD ALLOC: GraphicsBuffer[1] - impostor indirect draw args - owner: HectonOctahedralImpostorRenderer
                _argsMesh = null;
                _lastArgsInstanceCount = -1;
            }

            if (mesh == null)
                return;

            if (ReferenceEquals(_argsMesh, mesh) && _lastArgsInstanceCount == _instanceCount)
                return;

            int safeSubMesh = Mathf.Clamp(_subMeshIndex, 0, Mathf.Max(0, mesh.subMeshCount - 1));
            GraphicsBuffer.IndirectDrawIndexedArgs args = new GraphicsBuffer.IndirectDrawIndexedArgs
            {
                indexCountPerInstance = mesh.GetIndexCount(safeSubMesh),
                instanceCount = unchecked((uint)Mathf.Max(0, _instanceCount)),
                startIndex = mesh.GetIndexStart(safeSubMesh),
                baseVertexIndex = unchecked((uint)Mathf.Max(0, mesh.GetBaseVertex(safeSubMesh))),
                startInstance = 0u
            };
            if (!GraphicsBufferUploadUtility.TryUploadSingle(_argsBuffer, args))
                return;

            _argsMesh = mesh;
            _lastArgsInstanceCount = _instanceCount;
        }

        private Mesh ResolveQuadMesh()
        {
            return _quadMesh;
        }

        private Material ResolveMaterial()
        {
            return _material;
        }

        private bool TryBindDrawMaterial(Material material, bool useMatrixStream, GraphicsBuffer instanceBuffer)
        {
            HectonOctahedralImpostorData data = _impostorData;
            if (material == null || data == null || instanceBuffer == null)
                return false;

            Texture2D albedo = data.AlbedoDepthAtlas;
            Texture2D normal = data.NormalDepthAtlas;
            if (albedo == null || normal == null)
                return false;

            Vector2Int grid = data.AtlasGrid;
            Vector4 atlasGrid = new Vector4(
                Mathf.Max(1, grid.x),
                Mathf.Max(1, grid.y),
                1f / Mathf.Max(1, grid.x),
                1f / Mathf.Max(1, grid.y));
            float depthScale = Mathf.Max(0.01f, data.DepthScaleMeters);
            float quality = ResolveGlobalQualityWeight01();
            Vector3 floatingOffset = ResolveGlobalFloatingOffset();

            material.SetTexture(AlbedoDepthAtlasId, albedo);
            material.SetTexture(NormalDepthAtlasId, normal);
            material.SetVector(AtlasGridId, atlasGrid);
            material.SetFloat(DepthScaleMetersId, depthScale);
            material.SetFloat(GlobalQualityWeightId, quality);
            material.SetInt(UseVisibleMatrixStreamId, useMatrixStream ? 1 : 0);
            material.SetFloat(ImpostorTimeSecondsId, _impostorTimeSeconds);
            material.SetFloat(ImpostorFadeOutSecondsId, 1.5f);
            material.SetVector(GlobalFloatingOffsetId, floatingOffset);
            if (useMatrixStream)
                material.SetBuffer(VisibleInstancesId, instanceBuffer);
            else
                material.SetBuffer(ImpostorInstancesId, instanceBuffer);

            _lastQualityMilli = Mathf.RoundToInt(quality * 1000f);
            return true;
        }

        private static float ResolveGlobalQualityWeight01()
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.select(1f, quality, math.isfinite(quality)));
        }

        private Bounds ResolveDrawBounds()
        {
            if (_hasBoundsOverride)
                return _drawBounds;

            return new Bounds(transform.position + _fallbackBoundsCenterOffset, _fallbackBoundsSize);
        }

        private void ReportTelemetryIfDue()
        {
            int frame = _telemetryTickCounter;
            if (frame - _lastTelemetryTick < TelemetryIntervalFrames)
                return;

            _lastTelemetryTick = frame;
            CrashTelemetryBuffer.ReportActiveImpostors(_instanceCount, _lastQualityMilli, TelemetryHash);
        }

        private void ReleaseResources()
        {
            ReleaseGraphicsBuffer(ref _instanceBufferA);
            ReleaseGraphicsBuffer(ref _instanceBufferB);
            ReleaseGraphicsBuffer(ref _matrixSourceBufferA);
            ReleaseGraphicsBuffer(ref _matrixSourceBufferB);
            _activeInstanceBuffer = null;
            _activeMatrixSourceBuffer = null;
            _instanceUploadBufferIndex = 0;
            _matrixSourceUploadBufferIndex = 0;

            if (_argsBuffer != null)
            {
                _argsBuffer.Release();
                _argsBuffer = null;
            }

            _argsMesh = null;
            _instanceCount = 0;
            _debugBoundInstanceCount = 0;
            _lastArgsInstanceCount = -1;
            _matrixSourceCapacity = 0;
            _matrixSourceUploadedCount = 0;
            _useVisibleMatrixStream = false;
            _hasBoundsOverride = false;
        }

        private static Vector3 ResolveGlobalFloatingOffset()
        {
            return HectonFloatingOrigin.CurrentTotalOffset;
        }

        private static void ReleaseGraphicsBuffer(ref GraphicsBuffer buffer)
        {
            if (buffer == null)
                return;

            buffer.Release();
            buffer = null;
        }
    }
}
