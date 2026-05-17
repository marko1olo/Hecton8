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
        private const int QualityFlagLowTier = 1 << 0;
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
        private static readonly int QualityFlagsId = Shader.PropertyToID("_HectonImpostorQualityFlags");

        [Header("-- Rendering ----------------")]
        [SerializeField] private Mesh _quadMesh;
        [SerializeField] private Material _material;
        [SerializeField] private Shader _shader;
        [SerializeField] private HectonOctahedralImpostorData _impostorData;
        [SerializeField, Min(0)] private int _subMeshIndex;
        [SerializeField] private Camera _cameraOverride;

        [Header("-- Bounds -------------------")]
        [SerializeField] private Vector3 _fallbackBoundsCenterOffset = Vector3.zero;
        [SerializeField] private Vector3 _fallbackBoundsSize = new Vector3(3000f, 1600f, 3000f);

        [Header("-- Diagnostics --------------")]
        [SerializeField] private int _debugBoundInstanceCount;
        [SerializeField] private HectonQualityTier _debugQualityTier = HectonQualityTier.Unknown;

        private NativeArray<OctahedralImpostorInstance> _uploadedInstances;
        private GraphicsBuffer _instanceBuffer;
        private GraphicsBuffer _matrixSourceBuffer;
        private GraphicsBuffer _argsBuffer;
        private Mesh _argsMesh;
        private Mesh _runtimeQuadMesh;
        private Material _runtimeMaterial;
        private Bounds _drawBounds;
        private int _instanceCount;
        private int _lastArgsInstanceCount = -1;
        private int _lastQualityFlags = int.MinValue;
        private int _lastTelemetryFrame = -TelemetryIntervalFrames;
        private int _matrixSourceCapacity;
        private int _matrixSourceUploadedCount;
        private float _lastBoundsRadius = 1f;
        private bool _useVisibleMatrixStream;
        private bool _hasBoundsOverride;
        private bool _registeredTick;
        private IInstanceCullingService _instanceCullingService;
        private Texture2D _lastAlbedoAtlas;
        private Texture2D _lastNormalAtlas;

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
            RegisterTick();
        }

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
            _ = deltaTime;
            if (_instanceCount <= 0)
                return;

            Mesh mesh = ResolveQuadMesh();
            Material material = ResolveMaterial();
            if (mesh == null || material == null)
                return;

            ApplyDataToMaterial(material);
            ApplyQualityFlags(material);

            bool useMatrixStream = _useVisibleMatrixStream &&
                                   _instanceCullingService != null &&
                                   _instanceCullingService.VisibleInstancesBuffer != null &&
                                   _instanceCullingService.IndirectArgsBuffer != null;
            if (!useMatrixStream && _instanceBuffer == null)
                return;

            if (!useMatrixStream)
                EnsureIndirectArgsBuffer(mesh);

            Shader.SetGlobalInt(UseVisibleMatrixStreamId, useMatrixStream ? 1 : 0);
            Shader.SetGlobalFloat(ImpostorTimeSecondsId, Time.time);
            Shader.SetGlobalFloat(ImpostorFadeOutSecondsId, 1.5f);
            if (useMatrixStream)
                Shader.SetGlobalBuffer(VisibleInstancesId, _instanceCullingService.VisibleInstancesBuffer);
            else
                Shader.SetGlobalBuffer(ImpostorInstancesId, _instanceBuffer);
            Shader.SetGlobalVector(GlobalFloatingOffsetId, ResolveGlobalFloatingOffset());

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

            EnsureOwnedUploadCapacity(instanceCount);
            if (!_uploadedInstances.IsCreated || _instanceBuffer == null)
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
                _uploadedInstances[i] = instance;

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

            GraphicsBufferUploadUtility.UploadNativeArray(_instanceBuffer, _uploadedInstances, instanceCount);
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
            InstanceCullingDispatchDescriptor descriptor = new InstanceCullingDispatchDescriptor
            {
                AllInstancesBuffer = _matrixSourceBuffer,
                InstanceCount = instanceCount,
                BoundsRadius = _lastBoundsRadius,
                MaxCullDistanceMeters = Mathf.Max(1000f, _lastBoundsRadius * 64f),
                VramUsedMb = VRAMBudgetTracker.EstimatedVRAMBytes * GlobalTelemetryBus.BytesToMegabytes,
                QualityTier = ResolveCullingQualityTier(GlobalRegistry.ScalabilityTier),
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

            EnsureOwnedUploadCapacity(hlodCount);
            if (!_uploadedInstances.IsCreated || _instanceBuffer == null)
            {
                ClearBinding();
                return;
            }

            Bounds combinedBounds = default;
            bool hasCombinedBounds = false;
            Vector3 floatingOffset = ResolveGlobalFloatingOffset();
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
                _uploadedInstances[i] = instance;

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

            GraphicsBufferUploadUtility.UploadNativeArray(_instanceBuffer, _uploadedInstances, hlodCount);
            _instanceCount = hlodCount;
            _debugBoundInstanceCount = hlodCount;
            _drawBounds = hasCombinedBounds ? combinedBounds : new Bounds(transform.position + _fallbackBoundsCenterOffset, _fallbackBoundsSize);
            _hasBoundsOverride = hasCombinedBounds;
            _lastArgsInstanceCount = -1;
            EnsureIndirectArgsBuffer(ResolveQuadMesh());
        }

        private void BindMatricesAsOctahedralFallback(NativeArray<float4x4> matrices, int instanceCount)
        {
            EnsureOwnedUploadCapacity(instanceCount);
            if (!_uploadedInstances.IsCreated || _instanceBuffer == null)
            {
                ClearBinding();
                return;
            }

            Bounds combinedBounds = default;
            bool hasCombinedBounds = false;
            Vector3 floatingOffset = ResolveGlobalFloatingOffset();
            for (int i = 0; i < instanceCount; i++)
            {
                float4x4 matrix = matrices[i];
                Vector3 center = new Vector3(matrix.c3.x, matrix.c3.y, matrix.c3.z);
                Vector3 size = new Vector3(
                    Mathf.Max(0.5f, math.abs(matrix.c0.x)),
                    Mathf.Max(0.5f, math.abs(matrix.c1.y)),
                    Mathf.Max(0.5f, math.abs(matrix.c2.z)));
                float age01 = math.saturate((Time.time - matrix.c3.w) * ImpostorFadeSecondsRcp);
                float fadeAge = matrix.c0.w < 0f ? 1f - age01 : age01;
                uint flags = math.asuint(matrix.c2.w);
                _uploadedInstances[i] = OctahedralImpostorInstance.Create(
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

            GraphicsBufferUploadUtility.UploadNativeArray(_instanceBuffer, _uploadedInstances, instanceCount);
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

        private static InstanceCullingQualityTier ResolveCullingQualityTier(HectonQualityTier tier)
        {
            if (tier == HectonQualityTier.Low || tier == HectonQualityTier.Mx350)
                return InstanceCullingQualityTier.Low;
            if (tier == HectonQualityTier.High)
                return InstanceCullingQualityTier.High;
            if (tier == HectonQualityTier.Ultra)
                return InstanceCullingQualityTier.Ultra;
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

        private void EnsureOwnedUploadCapacity(int instanceCount)
        {
            int nextCapacity = Mathf.NextPowerOfTwo(Mathf.Max(1, instanceCount));
            if (_uploadedInstances.IsCreated &&
                _uploadedInstances.Length >= nextCapacity &&
                _instanceBuffer != null &&
                _instanceBuffer.count >= nextCapacity)
            {
                return;
            }

            if (_uploadedInstances.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_uploadedInstances);
                _uploadedInstances.Dispose();
            }

            if (_instanceBuffer != null)
            {
                _instanceBuffer.Release();
                _instanceBuffer = null;
            }

            _uploadedInstances = new NativeArray<OctahedralImpostorInstance>(nextCapacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<OctahedralImpostorInstance>[NextPowerOfTwo(requiredCount)] - impostor upload cache - owner: HectonOctahedralImpostorRenderer
            NativeMemorySentinel.RegisterNativeArray(_uploadedInstances, nameof(HectonOctahedralImpostorRenderer), nameof(_uploadedInstances), NativeAllocationLifetime.Session);
            _instanceBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<OctahedralImpostorInstance>(nextCapacity); // COLD ALLOC: GraphicsBuffer[NextPowerOfTwo(requiredCount)] - impostor instance buffer - owner: HectonOctahedralImpostorRenderer
        }

        private void EnsureIndirectArgsBuffer(Mesh mesh)
        {
            if (_argsBuffer == null)
                _argsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, GraphicsBuffer.UsageFlags.LockBufferForWrite, 1, GraphicsBuffer.IndirectDrawIndexedArgs.size); // COLD ALLOC: GraphicsBuffer[1] - impostor indirect draw args - owner: HectonOctahedralImpostorRenderer

            if (mesh == null)
                return;

            if (ReferenceEquals(_argsMesh, mesh) && _lastArgsInstanceCount == _instanceCount)
                return;

            int safeSubMesh = Mathf.Clamp(_subMeshIndex, 0, Mathf.Max(0, mesh.subMeshCount - 1));
            NativeArray<GraphicsBuffer.IndirectDrawIndexedArgs> argsWrite =
                _argsBuffer.LockBufferForWrite<GraphicsBuffer.IndirectDrawIndexedArgs>(0, 1);
            argsWrite[0] = new GraphicsBuffer.IndirectDrawIndexedArgs
            {
                indexCountPerInstance = mesh.GetIndexCount(safeSubMesh),
                instanceCount = unchecked((uint)Mathf.Max(0, _instanceCount)),
                startIndex = mesh.GetIndexStart(safeSubMesh),
                baseVertexIndex = unchecked((uint)Mathf.Max(0, mesh.GetBaseVertex(safeSubMesh))),
                startInstance = 0u
            };
            _argsBuffer.UnlockBufferAfterWrite<GraphicsBuffer.IndirectDrawIndexedArgs>(1);
            _argsMesh = mesh;
            _lastArgsInstanceCount = _instanceCount;
        }

        private Mesh ResolveQuadMesh()
        {
            if (_quadMesh != null)
                return _quadMesh;

            if (_runtimeQuadMesh != null)
                return _runtimeQuadMesh;

            _runtimeQuadMesh = new Mesh
            {
                name = "H8 Runtime Impostor Quad",
                hideFlags = HideFlags.HideAndDontSave
            };
            _runtimeQuadMesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f)
            }; // COLD ALLOC: Vector3[4] - fallback impostor quad vertices - owner: HectonOctahedralImpostorRenderer
            _runtimeQuadMesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f)
            }; // COLD ALLOC: Vector2[4] - fallback impostor quad UVs - owner: HectonOctahedralImpostorRenderer
            _runtimeQuadMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 }; // COLD ALLOC: int[6] - fallback impostor quad indices - owner: HectonOctahedralImpostorRenderer
            _runtimeQuadMesh.RecalculateBounds();
            return _runtimeQuadMesh;
        }

        private Material ResolveMaterial()
        {
            if (_material != null)
                return _material;

            if (_runtimeMaterial != null)
                return _runtimeMaterial;

            Shader shader = _shader != null ? _shader : Shader.Find("Hecton8/Environment/Hecton_OctahedralImpostor");
            if (shader == null)
                return null;

            _runtimeMaterial = new Material(shader)
            {
                name = "H8 Runtime Octahedral Impostor",
                hideFlags = HideFlags.HideAndDontSave
            }; // COLD ALLOC: Material[1] - fallback impostor material - owner: HectonOctahedralImpostorRenderer
            return _runtimeMaterial;
        }

        private void ApplyDataToMaterial(Material material)
        {
            HectonOctahedralImpostorData data = _impostorData;
            if (data == null)
                return;

            Texture2D albedo = data.AlbedoDepthAtlas;
            Texture2D normal = data.NormalDepthAtlas;

            if (!ReferenceEquals(_lastAlbedoAtlas, albedo) && albedo != null)
            {
                material.SetTexture(AlbedoDepthAtlasId, albedo);
                _lastAlbedoAtlas = albedo;
            }

            if (!ReferenceEquals(_lastNormalAtlas, normal) && normal != null)
            {
                material.SetTexture(NormalDepthAtlasId, normal);
                _lastNormalAtlas = normal;
            }
        }

        private void ApplyQualityFlags(Material material)
        {
            HectonQualityTier tier = GlobalRegistry.ScalabilityTier;
            _debugQualityTier = tier;
            int flags = HectonChunkImpostorResidency.IsLowTier(tier) ? QualityFlagLowTier : 0;
            if (_lastQualityFlags == flags)
                return;

            material.SetInt(QualityFlagsId, flags);
            _lastQualityFlags = flags;
        }

        private Bounds ResolveDrawBounds()
        {
            if (_hasBoundsOverride)
                return _drawBounds;

            return new Bounds(transform.position + _fallbackBoundsCenterOffset, _fallbackBoundsSize);
        }

        private void ReportTelemetryIfDue()
        {
            int frame = Time.frameCount;
            if (frame - _lastTelemetryFrame < TelemetryIntervalFrames)
                return;

            _lastTelemetryFrame = frame;
            CrashTelemetryBuffer.ReportActiveImpostors(_instanceCount, _lastQualityFlags, TelemetryHash);
        }

        private void ReleaseResources()
        {
            if (_uploadedInstances.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_uploadedInstances);
                _uploadedInstances.Dispose();
            }

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

            if (_runtimeMaterial != null)
            {
                Destroy(_runtimeMaterial);
                _runtimeMaterial = null;
            }

            if (_runtimeQuadMesh != null)
            {
                Destroy(_runtimeQuadMesh);
                _runtimeQuadMesh = null;
            }
        }

        private static Vector3 ResolveGlobalFloatingOffset()
        {
            return HectonMapMagicVegetationBridge.GlobalTotalUniverseOffset;
        }
    }
}
