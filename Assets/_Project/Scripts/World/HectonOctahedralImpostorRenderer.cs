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
    public sealed class HectonOctahedralImpostorRenderer : MonoBehaviour, IUpdatable, IOriginShiftListener, IStreamingHlodMatrixRenderer
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

        private GraphicsBuffer _instanceBuffer;
        private GraphicsBuffer _matrixSourceBuffer;
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
        private float _lastBoundsRadius = 1f;
        private float _impostorTimeSeconds;
        private bool _useVisibleMatrixStream;
        private bool _hasBoundsOverride;
        private bool _registeredTick;
        private IInstanceCullingService _instanceCullingService;
        private Texture2D _lastAlbedoAtlas;
        private Texture2D _lastNormalAtlas;
        private Vector4 _lastAtlasGrid = new Vector4(-1f, -1f, -1f, -1f);
        private float _lastDepthScaleMeters = -1f;
        private float _lastGlobalQualityWeight = -1f;
        private Vector3 _lastGlobalFloatingOffset;
        private Material _lastStaticMaterial;
        private Material _lastQualityMaterial;
        private Material _lastFloatingOffsetMaterial;
        private HectonOctahedralImpostorData _lastStaticData;
        private bool _staticMaterialDirty = true;
        private bool _staticPayloadValid;
        private bool _floatingOffsetDirty = true;

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
            InvalidateMaterialCaches();
            RegisterTick();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            InvalidateMaterialCaches();
        }
#endif

        private void OnDisable()
        {
            HectonFloatingOrigin.UnregisterListener(this);
            UnregisterTick();
        }

        private void OnDestroy()
        {
            HectonFloatingOrigin.UnregisterListener(this);
            ReleaseResources();
        }

        public void Tick(float deltaTime)
        {
            _telemetryTickCounter++;
            if (deltaTime > 0f && math.isfinite(deltaTime))
                _impostorTimeSeconds += math.min(deltaTime, 0.25f);

            if (_instanceCount <= 0)
                return;

            Mesh mesh = ResolveQuadMesh();
            Material material = ResolveMaterial();
            if (mesh == null || material == null)
                return;

            if (!ApplyStaticDataToMaterialIfNeeded(material))
                return;

            ApplyQualityWeight(material);

            bool useMatrixStream = _useVisibleMatrixStream &&
                                   _instanceCullingService != null &&
                                   _instanceCullingService.VisibleInstancesBuffer != null &&
                                   _instanceCullingService.IndirectArgsBuffer != null;
            if (!useMatrixStream && _instanceBuffer == null)
                return;

            if (!useMatrixStream)
                EnsureIndirectArgsBuffer(mesh);

            material.SetInt(UseVisibleMatrixStreamId, useMatrixStream ? 1 : 0);
            material.SetFloat(ImpostorTimeSecondsId, _impostorTimeSeconds);
            material.SetFloat(ImpostorFadeOutSecondsId, 1.5f);
            if (useMatrixStream)
                material.SetBuffer(VisibleInstancesId, _instanceCullingService.VisibleInstancesBuffer);
            else
                material.SetBuffer(ImpostorInstancesId, _instanceBuffer);
            ApplyGlobalFloatingOffset(material);

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
            if (_instanceBuffer == null || !_instanceBuffer.IsValid())
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

            GraphicsBufferUploadUtility.UploadNativeArray(_instanceBuffer, instances, instanceCount);
            _instanceCount = instanceCount;
            _debugBoundInstanceCount = instanceCount;
            _drawBounds = hasCombinedBounds ? combinedBounds : new Bounds(transform.position + _fallbackBoundsCenterOffset, _fallbackBoundsSize);
            _hasBoundsOverride = hasCombinedBounds;
            _lastArgsInstanceCount = -1;
            EnsureIndirectArgsBuffer(ResolveQuadMesh());
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
            if (_matrixSourceBuffer == null || !_matrixSourceBuffer.IsValid())
            {
                BindMatricesAsOctahedralFallback(matrices, instanceCount);
                return;
            }

            bool needsUpload = forceUpload || _matrixSourceUploadedCount != instanceCount;
            if (needsUpload)
            {
                GraphicsBufferUploadUtility.UploadNativeArray(_matrixSourceBuffer, matrices, instanceCount);
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
                if (needsUpload || wasUsingVisibleMatrixStream || _instanceBuffer == null || _instanceCount != instanceCount)
                    BindMatricesAsOctahedralFallback(matrices, instanceCount);
                else
                    EnsureIndirectArgsBuffer(mesh);
                return;
            }

            int safeSubMesh = Mathf.Clamp(_subMeshIndex, 0, Mathf.Max(0, mesh.subMeshCount - 1));
            float globalQualityWeight = ResolveGlobalQualityWeight01();
            InstanceCullingDispatchDescriptor descriptor = new InstanceCullingDispatchDescriptor
            {
                AllInstancesBuffer = _matrixSourceBuffer,
                InstanceCount = instanceCount,
                BoundsRadius = _lastBoundsRadius,
                MaxCullDistanceMeters = Mathf.Max(1000f, _lastBoundsRadius * 64f),
                VramUsedMb = VRAMBudgetTracker.EstimatedVRAMBytes * GlobalTelemetryBus.BytesToMegabytes,
                GlobalQualityWeight = globalQualityWeight,
                QualityTier = ResolveCullingQualityTier(globalQualityWeight),
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
                if (needsUpload || wasUsingVisibleMatrixStream || _instanceBuffer == null || _instanceCount != instanceCount)
                    BindMatricesAsOctahedralFallback(matrices, instanceCount);
                else
                    EnsureIndirectArgsBuffer(mesh);
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
            if (_instanceBuffer == null || !_instanceBuffer.IsValid())
            {
                ClearBinding();
                return;
            }

            Bounds combinedBounds = default;
            bool hasCombinedBounds = false;
            Vector3 floatingOffset = ResolveGlobalFloatingOffset();
            NativeArray<OctahedralImpostorInstance> upload = _instanceBuffer.LockBufferForWrite<OctahedralImpostorInstance>(0, hlodCount);
            try
            {
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
            }
            finally
            {
                _instanceBuffer.UnlockBufferAfterWrite<OctahedralImpostorInstance>(hlodCount);
            }

            _instanceCount = hlodCount;
            _debugBoundInstanceCount = hlodCount;
            _drawBounds = hasCombinedBounds ? combinedBounds : new Bounds(transform.position + _fallbackBoundsCenterOffset, _fallbackBoundsSize);
            _hasBoundsOverride = hasCombinedBounds;
            _lastArgsInstanceCount = -1;
            EnsureIndirectArgsBuffer(ResolveQuadMesh());
        }

        private void BindMatricesAsOctahedralFallback(NativeArray<float4x4> matrices, int instanceCount)
        {
            EnsureInstanceBufferCapacity(instanceCount);
            if (_instanceBuffer == null || !_instanceBuffer.IsValid())
            {
                ClearBinding();
                return;
            }

            Bounds combinedBounds = default;
            bool hasCombinedBounds = false;
            Vector3 floatingOffset = ResolveGlobalFloatingOffset();
            NativeArray<OctahedralImpostorInstance> upload = _instanceBuffer.LockBufferForWrite<OctahedralImpostorInstance>(0, instanceCount);
            try
            {
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
            }
            finally
            {
                _instanceBuffer.UnlockBufferAfterWrite<OctahedralImpostorInstance>(instanceCount);
            }

            _useVisibleMatrixStream = false;
            _instanceCount = instanceCount;
            _debugBoundInstanceCount = instanceCount;
            _drawBounds = hasCombinedBounds ? combinedBounds : new Bounds(transform.position + _fallbackBoundsCenterOffset, _fallbackBoundsSize);
            _hasBoundsOverride = hasCombinedBounds;
            _lastArgsInstanceCount = -1;
            EnsureIndirectArgsBuffer(ResolveQuadMesh());
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
            if (_matrixSourceBuffer != null &&
                _matrixSourceBuffer.IsValid() &&
                _matrixSourceCapacity >= nextCapacity)
            {
                return;
            }

            if (_matrixSourceBuffer != null)
            {
                _matrixSourceBuffer.Release();
                _matrixSourceBuffer = null;
            }

            _matrixSourceBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float4x4>(nextCapacity); // COLD ALLOC: GraphicsBuffer[NextPowerOfTwo(requiredCount)] - HLOD matrix source for instance culling - owner: HectonOctahedralImpostorRenderer
            _matrixSourceCapacity = nextCapacity;
            _matrixSourceUploadedCount = 0;
        }

        private IInstanceCullingService ResolveInstanceCullingService()
        {
            if (_instanceCullingService != null)
                return _instanceCullingService;

            _instanceCullingService = GlobalRegistry.InstanceCulling;
            return _instanceCullingService;
        }

        private static InstanceCullingQualityTier ResolveCullingQualityTier(float globalQualityWeight)
        {
            float q = math.saturate(math.select(1f, globalQualityWeight, math.isfinite(globalQualityWeight)));
            if (q < 0.25f)
                return InstanceCullingQualityTier.Low;
            if (q >= 0.82f)
                return InstanceCullingQualityTier.Ultra;
            if (q >= 0.58f)
                return InstanceCullingQualityTier.High;
            return InstanceCullingQualityTier.Middle;
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
            _floatingOffsetDirty = true;
        }

        private void InvalidateMaterialCaches()
        {
            _staticMaterialDirty = true;
            _staticPayloadValid = false;
            _floatingOffsetDirty = true;
            _lastQualityMaterial = null;
            _lastFloatingOffsetMaterial = null;
        }

        private void RegisterTick()
        {
            if (_registeredTick || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
            _registeredTick = GlobalRegistry.Updatables.Contains(this);
        }

        private void UnregisterTick()
        {
            if (!_registeredTick)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            _registeredTick = false;
        }

        private void EnsureInstanceBufferCapacity(int instanceCount)
        {
            int nextCapacity = Mathf.NextPowerOfTwo(Mathf.Max(1, instanceCount));
            if (_instanceBuffer != null &&
                _instanceBuffer.IsValid() &&
                _instanceBuffer.count >= nextCapacity)
            {
                return;
            }

            if (_instanceBuffer != null)
            {
                _instanceBuffer.Release();
                _instanceBuffer = null;
            }

            _instanceBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<OctahedralImpostorInstance>(nextCapacity); // COLD ALLOC: GraphicsBuffer[NextPowerOfTwo(requiredCount)] - impostor instance buffer - owner: HectonOctahedralImpostorRenderer
        }

        private void EnsureIndirectArgsBuffer(Mesh mesh)
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
            NativeArray<GraphicsBuffer.IndirectDrawIndexedArgs> argsWrite =
                _argsBuffer.LockBufferForWrite<GraphicsBuffer.IndirectDrawIndexedArgs>(0, 1);
            try
            {
                argsWrite[0] = new GraphicsBuffer.IndirectDrawIndexedArgs
                {
                    indexCountPerInstance = mesh.GetIndexCount(safeSubMesh),
                    instanceCount = unchecked((uint)Mathf.Max(0, _instanceCount)),
                    startIndex = mesh.GetIndexStart(safeSubMesh),
                    baseVertexIndex = unchecked((uint)Mathf.Max(0, mesh.GetBaseVertex(safeSubMesh))),
                    startInstance = 0u
                };
            }
            finally
            {
                _argsBuffer.UnlockBufferAfterWrite<GraphicsBuffer.IndirectDrawIndexedArgs>(1);
            }

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

        private bool ApplyStaticDataToMaterialIfNeeded(Material material)
        {
            HectonOctahedralImpostorData data = _impostorData;
            if (!_staticMaterialDirty &&
                ReferenceEquals(_lastStaticMaterial, material) &&
                ReferenceEquals(_lastStaticData, data))
            {
                return _staticPayloadValid;
            }

            _staticMaterialDirty = false;
            _staticPayloadValid = false;
            _lastStaticMaterial = material;
            _lastStaticData = data;
            _lastAlbedoAtlas = null;
            _lastNormalAtlas = null;
            _lastAtlasGrid = new Vector4(-1f, -1f, -1f, -1f);
            _lastDepthScaleMeters = -1f;

            if (data == null)
                return false;

            Texture2D albedo = data.AlbedoDepthAtlas;
            Texture2D normal = data.NormalDepthAtlas;
            if (albedo == null || normal == null)
                return false;

            if (!ReferenceEquals(_lastAlbedoAtlas, albedo))
            {
                material.SetTexture(AlbedoDepthAtlasId, albedo);
                _lastAlbedoAtlas = albedo;
            }

            if (!ReferenceEquals(_lastNormalAtlas, normal))
            {
                material.SetTexture(NormalDepthAtlasId, normal);
                _lastNormalAtlas = normal;
            }

            Vector2Int grid = data.AtlasGrid;
            Vector4 atlasGrid = new Vector4(
                Mathf.Max(1, grid.x),
                Mathf.Max(1, grid.y),
                1f / Mathf.Max(1, grid.x),
                1f / Mathf.Max(1, grid.y));
            if (_lastAtlasGrid != atlasGrid)
            {
                material.SetVector(AtlasGridId, atlasGrid);
                _lastAtlasGrid = atlasGrid;
            }

            float depthScale = Mathf.Max(0.01f, data.DepthScaleMeters);
            if (!Mathf.Approximately(_lastDepthScaleMeters, depthScale))
            {
                material.SetFloat(DepthScaleMetersId, depthScale);
                _lastDepthScaleMeters = depthScale;
            }

            _staticPayloadValid = true;
            return true;
        }

        private void ApplyQualityWeight(Material material)
        {
            float quality = ResolveGlobalQualityWeight01();
            if (!ReferenceEquals(_lastQualityMaterial, material) || !Mathf.Approximately(_lastGlobalQualityWeight, quality))
            {
                material.SetFloat(GlobalQualityWeightId, quality);
                _lastGlobalQualityWeight = quality;
                _lastQualityMaterial = material;
            }

            _lastQualityMilli = Mathf.RoundToInt(quality * 1000f);
        }

        private void ApplyGlobalFloatingOffset(Material material)
        {
            Vector3 floatingOffset = ResolveGlobalFloatingOffset();
            if (!_floatingOffsetDirty &&
                ReferenceEquals(_lastFloatingOffsetMaterial, material) &&
                _lastGlobalFloatingOffset == floatingOffset)
            {
                return;
            }

            material.SetVector(GlobalFloatingOffsetId, floatingOffset);
            _lastGlobalFloatingOffset = floatingOffset;
            _lastFloatingOffsetMaterial = material;
            _floatingOffsetDirty = false;
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
            if (_instanceBuffer != null)
            {
                _instanceBuffer.Release();
                _instanceBuffer = null;
            }

            if (_matrixSourceBuffer != null)
            {
                _matrixSourceBuffer.Release();
                _matrixSourceBuffer = null;
                _matrixSourceCapacity = 0;
            }

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
            _staticPayloadValid = false;
        }

        private static Vector3 ResolveGlobalFloatingOffset()
        {
            return HectonFloatingOrigin.CurrentTotalOffset;
        }
    }
}
