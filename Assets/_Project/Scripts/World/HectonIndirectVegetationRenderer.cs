using Hecton8.Core;
using Hecton8.Gameplay;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.World
{
    /// <summary>
    /// Indirect renderer for dense procedural vegetation driven by external GPU buffers.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-90)]
    public sealed class HectonIndirectVegetationRenderer : MonoBehaviour, ITickable
    {
        /// <summary>Stride of one Matrix4x4 entry expected in the external instance matrix buffer.</summary>
        public const int InstanceMatrixStride = 64;

        /// <summary>Stride of one <see cref="HectonVegetationInstanceData"/> entry expected in the instance metadata buffer.</summary>
        public const int InstanceDataStride = HectonVegetationInstanceData.Stride;

        private const int IndirectArgsCount = 5;
        private const int VisibleIndexStride = sizeof(uint);
        private const int ThreadsPerGroup = 64;
#if UNITY_EDITOR
        private const string ComputeShaderAssetPath = "Assets/_Project/Art/Shaders/FloraCulling.compute";
        private const string DepthShaderAssetPath = "Assets/_Project/Art/Shaders/Hecton_IndirectVegetationDepthOnly.shader";
        private const string ShadowShaderAssetPath = "Assets/_Project/Art/Shaders/Hecton_IndirectVegetationShadow.shader";
        private const string MotionShaderAssetPath = "Assets/_Project/Art/Shaders/Hecton_IndirectVegetationMotionVectors.shader";
#endif

        private static readonly int _InstanceMatricesId = Shader.PropertyToID("_HectonInstanceMatrices");
        private static readonly int _InstanceDataId = Shader.PropertyToID("_HectonVegetationInstanceData");
        private static readonly int _VisibleInstanceIndicesId = Shader.PropertyToID("_HectonVisibleInstanceIndices");
        private static readonly int _ChunkWorldOffsetId = Shader.PropertyToID("_ChunkWorldOffset");
        private static readonly int _GlobalFloatingOffsetId = Shader.PropertyToID("_GlobalFloatingOffset");
        private static readonly int _LodPassModeId = Shader.PropertyToID("_HectonLodPassMode");
        private static readonly int _LodNearDistanceId = Shader.PropertyToID("_HectonLodNearDistance");
        private static readonly int _LodFarDistanceId = Shader.PropertyToID("_HectonLodFarDistance");
        private static readonly int _LodTransitionRangeId = Shader.PropertyToID("_HectonLodTransitionRange");
        private static readonly int _ImpostorWidthId = Shader.PropertyToID("_HectonImpostorWidth");
        private static readonly int _ImpostorHeightId = Shader.PropertyToID("_HectonImpostorHeight");
        private static readonly int _SourceInstanceCountId = Shader.PropertyToID("_HectonSourceInstanceCount");
        private static readonly int _ViewProjectionId = Shader.PropertyToID("_HectonViewProjection");
        private static readonly int _ViewMatrixId = Shader.PropertyToID("_HectonViewMatrix");
        private static readonly int _CameraPositionId = Shader.PropertyToID("_HectonCameraPosition");
        private static readonly int _CameraDepthTextureId = Shader.PropertyToID("_HectonCameraDepthTexture");
        private static readonly int _OcclusionEnabledId = Shader.PropertyToID("_HectonOcclusionEnabled");
        private static readonly int _OcclusionDepthBiasId = Shader.PropertyToID("_HectonOcclusionDepthBias");
        private static readonly int _OcclusionZBufferParamsId = Shader.PropertyToID("_HectonZBufferParams");
        private static readonly int _GlobalCameraDepthTextureId = Shader.PropertyToID("_CameraDepthTexture");
        private static readonly int _GlobalZBufferParamsId = Shader.PropertyToID("_ZBufferParams");
        private static readonly int _DarknessCullEnabledId = Shader.PropertyToID("_HectonDarknessCullEnabled");
        private static readonly int _DarknessBiolumThresholdId = Shader.PropertyToID("_HectonDarknessBiolumThreshold");
        private static readonly int _ScooterHeadlightCountId = Shader.PropertyToID("_HectonScooterHeadlightCount");
        private static readonly int _ScooterHeadlightPositionsWsId = Shader.PropertyToID("_HectonScooterHeadlightPositionsWS");
        private static readonly int _ScooterHeadlightDirectionsWsId = Shader.PropertyToID("_HectonScooterHeadlightDirectionsWS");
        private static readonly int _ScooterHeadlightColorsId = Shader.PropertyToID("_HectonScooterHeadlightColors");
        private static readonly int _ScooterHeadlightConeDataId = Shader.PropertyToID("_HectonScooterHeadlightConeData");
        private static readonly int _FloorBiolumStrengthId = Shader.PropertyToID("_HectonFloorBiolumStrength");
        private static readonly int _OceanBiolumStrengthId = Shader.PropertyToID("_HectonOceanBiolumStrength");
        private static readonly int _GlobalBiolumIntensityId = Shader.PropertyToID("_BiolumIntensity");
        private static readonly int _SourceMatricesId = Shader.PropertyToID("_HectonSourceInstanceMatrices");
        private static readonly int _SourceDataId = Shader.PropertyToID("_HectonSourceVegetationInstanceData");
        private static readonly int _VisibleIndicesLod0Id = Shader.PropertyToID("_HectonVisibleInstanceIndicesLOD0");
        private static readonly int _VisibleIndicesLod1Id = Shader.PropertyToID("_HectonVisibleInstanceIndicesLOD1");
        private static readonly int _VisibleIndicesShadowId = Shader.PropertyToID("_HectonVisibleInstanceIndicesShadow");
        private static readonly int _PreviousCameraPositionId = Shader.PropertyToID("_HectonPreviousCameraPosition");
        private const int MaxScooterHeadlights = 2;

        [Header("Rendering")]
        [SerializeField]
        [Tooltip("Material that consumes the indirect vegetation matrix and metadata buffers in the shader.")]
        private Material _material;

        [SerializeField]
        [Tooltip("Compute shader that performs GPU frustum culling and per-instance LOD classification.")]
        private ComputeShader _cullingCompute;

        [SerializeField]
        [Tooltip("Hidden depth-only shader used to prime the Z buffer before the expensive forward vegetation pass.")]
        private Shader _depthOnlyShader;

        [SerializeField]
        [Tooltip("Hidden shadow-only shader used for shadow-caster draws with a dedicated GPU shadow culling buffer.")]
        private Shader _shadowCasterShader;

        [SerializeField]
        [Tooltip("Hidden motion-vector shader used to write stable motion vectors for indirect vegetation instances.")]
        private Shader _motionVectorShader;

        [SerializeField]
        [Tooltip("Optional authored near mesh. If empty, a strip mesh is generated once at runtime.")]
        private Mesh _mesh;

        [SerializeField]
        [Tooltip("Submesh index rendered through the indirect draw calls.")]
        private int _subMeshIndex;

        [SerializeField]
        [Tooltip("Optional camera override. Leave null to render in all cameras.")]
        private Camera _cameraOverride;

        [SerializeField]
        [Tooltip("Shadow mode for the near indirect vegetation draw call.")]
        private ShadowCastingMode _shadowCastingMode = ShadowCastingMode.Off;

        [SerializeField]
        [Tooltip("Whether the near indirect vegetation draw call should receive shadows.")]
        private bool _receiveShadows;

        [SerializeField]
        [Tooltip("Shadow mode for the far impostor draw call.")]
        private ShadowCastingMode _impostorShadowCastingMode = ShadowCastingMode.Off;

        [SerializeField]
        [Tooltip("Whether the far impostor draw call should receive shadows.")]
        private bool _impostorReceiveShadows;

        [SerializeField]
        [Tooltip("When enabled, a dedicated depth-only indirect draw primes the Z buffer before forward lighting to reduce alpha-tested overdraw.")]
        private bool _enableDepthPrepass = true;

        [SerializeField]
        [Tooltip("When enabled, a dedicated shadow-only indirect draw uses its own GPU culling buffer instead of letting the forward draw populate shadow maps.")]
        private bool _enableShadowCasterDraw = true;

        [SerializeField]
        [Tooltip("Enables a dedicated motion-vector draw for indirect vegetation to reduce TAA and motion-blur artifacts.")]
        private bool _enableMotionVectorDraw = true;

        [Header("Runtime Mesh")]
        [SerializeField]
        [Tooltip("Generates a single strip mesh once at runtime when no authored near mesh is assigned.")]
        private bool _generateMeshAtRuntime = true;

        [SerializeField, Range(4, 6)]
        [Tooltip("Strip segment count. User task requires 4-6 segments.")]
        private int _segmentCount = 5;

        [SerializeField, Min(0.05f)]
        [Tooltip("Generated strip height.")]
        private float _stripHeight = 1.8f;

        [SerializeField, Min(0.005f)]
        [Tooltip("Generated strip width at the base.")]
        private float _stripBaseWidth = 0.12f;

        [SerializeField, Min(0.001f)]
        [Tooltip("Generated strip width at the tip.")]
        private float _stripTipWidth = 0.015f;

        [Header("Impostor Cards")]
        [SerializeField]
        [Tooltip("Optional authored far impostor card mesh. If empty, a quad is generated once at runtime.")]
        private Mesh _impostorMesh;

        [SerializeField]
        [Tooltip("Generates a unit vertical card once at runtime when no authored impostor mesh is assigned.")]
        private bool _generateImpostorMeshAtRuntime = true;

        [SerializeField, Min(0.25f)]
        [Tooltip("Billboard card width multiplier passed into the shader.")]
        private float _impostorWidth = 1.1f;

        [SerializeField, Min(0.25f)]
        [Tooltip("Billboard card height multiplier passed into the shader.")]
        private float _impostorHeight = 1f;

        [Header("LOD")]
        [SerializeField, Range(10f, 80f)]
        [Tooltip("Near band end distance in meters. Real strip geometry renders only inside this radius.")]
        private float _nearLodDistance = 50f;

        [SerializeField, Range(60f, 180f)]
        [Tooltip("Far band end distance in meters. Billboard cards render only up to this radius.")]
        private float _farLodDistance = 150f;

        [SerializeField, Range(0.5f, 20f)]
        [Tooltip("Cross-fade range around the near/far band thresholds.")]
        private float _lodTransitionRange = 6f;

        [Header("GPU Occlusion")]
        [SerializeField]
        [Tooltip("Enables depth-texture based occlusion rejection inside the compute culling pass when a camera depth texture is available.")]
        private bool _enableDepthOcclusion = true;

        [SerializeField, Range(0.05f, 2f)]
        [Tooltip("Depth bias in view-space meters used to avoid false occlusion rejection on grazing surfaces.")]
        private float _occlusionDepthBias = 0.35f;

        [Header("Darkness Culling")]
        [SerializeField]
        [Tooltip("Rejects flora instances that are outside the published scooter headlights and below the global biolum threshold.")]
        private bool _enableDarknessCulling = true;

        [SerializeField, Range(0.001f, 0.25f)]
        [Tooltip("Minimum combined global biolum scalar required to keep completely unlit instances alive.")]
        private float _darknessBiolumThreshold = 0.05f;

        [Header("Legacy Fallback")]
        [SerializeField]
        [Tooltip("Fallback vegetation type used when no external instance metadata buffer is bound.")]
        private HectonVegetationInstanceType _legacyFallbackType = HectonVegetationInstanceType.Grass;

        [Header("Draw Bounds")]
        [SerializeField]
        [Tooltip("Local center offset used when no explicit bounds override is supplied.")]
        private Vector3 _boundsCenterOffset = Vector3.zero;

        [SerializeField]
        [Tooltip("Fallback draw bounds size used when no explicit bounds override is supplied.")]
        private Vector3 _boundsSize = new Vector3(128f, 32f, 128f);

        private Mesh _generatedMesh;
        private Mesh _generatedImpostorMesh;
        private ComputeBuffer _instanceMatrixBuffer;
        private ComputeBuffer _instanceDataBuffer;
        private ComputeBuffer _legacyInstanceDataBuffer;
        private ComputeBuffer _uploadedInstanceMatrixBuffer;
        private ComputeBuffer _uploadedInstanceDataBuffer;
        private ComputeBuffer _visibleIndexBufferLod0;
        private ComputeBuffer _visibleIndexBufferLod1;
        private ComputeBuffer _visibleIndexBufferShadow;
        private ComputeBuffer _nearIndirectArgsBuffer;
        private ComputeBuffer _farIndirectArgsBuffer;
        private ComputeBuffer _shadowIndirectArgsBuffer;
        private MaterialPropertyBlock _nearPropertyBlock;
        private MaterialPropertyBlock _farPropertyBlock;
        private MaterialPropertyBlock _depthNearPropertyBlock;
        private MaterialPropertyBlock _depthFarPropertyBlock;
        private MaterialPropertyBlock _shadowPropertyBlock;
        private MaterialPropertyBlock _motionNearPropertyBlock;
        private MaterialPropertyBlock _motionFarPropertyBlock;
        private IHectonIndirectVegetationBufferSource _bufferSource;
        private Bounds _explicitBounds;
        private bool _hasBoundsOverride;
        private bool _isRegistered;
        private bool _argsDirty = true;
        private bool _matrixBindingDirty = true;
        private bool _dataBindingDirty = true;
        private bool _visibleBindingDirty = true;
        private bool _legacyDataDirty = true;
        private int _instanceCount;
        private int _cullingKernelIndex = -1;
        private int _shadowCullingKernelIndex = -1;
        private Camera _cachedCullCamera;
        private Material _depthOnlyMaterial;
        private Material _shadowCasterMaterial;
        private Material _motionVectorMaterial;
        private Vector3 _previousMotionCameraPosition;
        private Camera _previousMotionCamera;
        private bool _hasPreviousMotionCameraPosition;
        private Vector4 _lastGlobalFloatingOffset = new Vector4(float.NaN, float.NaN, float.NaN, float.NaN);
        private PlayerToolManager _playerToolManager;
        private float _nextToolManagerResolveTime;

        private Vector4[] _scooterHeadlightPositionsWs;
        private Vector4[] _scooterHeadlightDirectionsWs;
        private Vector4[] _scooterHeadlightColors;
        private Vector4[] _scooterHeadlightConeData;

        // COLD ALLOC: Camera[8] - camera discovery cache for GPU culling dispatch - owner: HectonIndirectVegetationRenderer
        private readonly Camera[] _cameraSearchCache = new Camera[8];

        // COLD ALLOC: uint[5] - near-pass indirect draw arguments payload - owner: HectonIndirectVegetationRenderer
        private readonly uint[] _nearIndirectArgs = new uint[IndirectArgsCount];
            // COLD ALLOC: uint[5] - far-pass indirect draw arguments payload - owner: HectonIndirectVegetationRenderer
            private readonly uint[] _farIndirectArgs = new uint[IndirectArgsCount];

        private HectonVegetationInstanceData[] _legacyInstanceData;

        /// <summary>True when an external matrix buffer is currently bound.</summary>
        public bool HasMatrixBuffer => _instanceMatrixBuffer != null;

        /// <summary>True when either an external or fallback instance metadata buffer is currently bound.</summary>
        public bool HasInstanceDataBuffer => _instanceDataBuffer != null || _legacyInstanceDataBuffer != null;

        /// <summary>Current active instance count published into the indirect args payload.</summary>
        public int BoundInstanceCount => _instanceCount;

        /// <summary>Configured distance where full strip geometry stops rendering.</summary>
        public float NearLodDistance => _nearLodDistance;

        /// <summary>Configured distance where impostor rendering ends and the pass culls completely.</summary>
        public float FarLodDistance => _farLodDistance;

        /// <summary>True when the far impostor pass is currently enabled.</summary>
        public bool UsesImpostorPass => _farLodDistance > _nearLodDistance;

        /// <summary>True when this renderer is currently consuming caller-provided array uploads staged into owned GPU buffers.</summary>
        public bool UsesOwnedUploadBuffers => _instanceMatrixBuffer == _uploadedInstanceMatrixBuffer;

        /// <summary>Approximate VRAM footprint in bytes for the renderer-owned culling and indirect argument buffers.</summary>
        public long GetVRAMEstimation()
        {
            long totalBytes = 0L;
            totalBytes += EstimateComputeBufferBytes(_visibleIndexBufferLod0);
            totalBytes += EstimateComputeBufferBytes(_visibleIndexBufferLod1);
            totalBytes += EstimateComputeBufferBytes(_visibleIndexBufferShadow);
            totalBytes += EstimateComputeBufferBytes(_nearIndirectArgsBuffer);
            totalBytes += EstimateComputeBufferBytes(_farIndirectArgsBuffer);
            totalBytes += EstimateComputeBufferBytes(_shadowIndirectArgsBuffer);
            return totalBytes;
        }

        private void Awake()
        {
            _nearLodDistance = Mathf.Max(1f, _nearLodDistance);
            _farLodDistance = Mathf.Max(_nearLodDistance, _farLodDistance);
            _lodTransitionRange = Mathf.Max(0.5f, _lodTransitionRange);
            TryAutoAssignAssets();

            if (_material == null || _cullingCompute == null)
            {
                Debug.LogError("[HectonIndirectVegetationRenderer] Material and culling compute shader are required.", this);
                enabled = false;
                return;
            }

            if (_generateMeshAtRuntime || _mesh == null)
            {
                _generatedMesh = HectonProceduralVegetationStripBuilder.Build(
                    $"{nameof(HectonIndirectVegetationRenderer)}_Strip",
                    _segmentCount,
                    _stripHeight,
                    _stripBaseWidth,
                    _stripTipWidth);
            }

            if ((_generateImpostorMeshAtRuntime || _impostorMesh == null) && _farLodDistance > _nearLodDistance)
                _generatedImpostorMesh = BuildImpostorCardMesh();

            if (ResolveNearRenderMesh() == null)
            {
                Debug.LogError("[HectonIndirectVegetationRenderer] No near render mesh resolved.", this);
                enabled = false;
                return;
            }

            // COLD ALLOC: MaterialPropertyBlock[1] - near indirect vegetation draw property block - owner: HectonIndirectVegetationRenderer
            _nearPropertyBlock = new MaterialPropertyBlock();
            // COLD ALLOC: MaterialPropertyBlock[1] - far indirect vegetation draw property block - owner: HectonIndirectVegetationRenderer
            _farPropertyBlock = new MaterialPropertyBlock();
            // COLD ALLOC: MaterialPropertyBlock[1] - depth-only near draw property block - owner: HectonIndirectVegetationRenderer
            _depthNearPropertyBlock = new MaterialPropertyBlock();
            // COLD ALLOC: MaterialPropertyBlock[1] - depth-only far draw property block - owner: HectonIndirectVegetationRenderer
            _depthFarPropertyBlock = new MaterialPropertyBlock();
            // COLD ALLOC: MaterialPropertyBlock[1] - shadow indirect vegetation draw property block - owner: HectonIndirectVegetationRenderer
            _shadowPropertyBlock = new MaterialPropertyBlock();
            // COLD ALLOC: MaterialPropertyBlock[1] - motion-vector near draw property block - owner: HectonIndirectVegetationRenderer
            _motionNearPropertyBlock = new MaterialPropertyBlock();
            // COLD ALLOC: MaterialPropertyBlock[1] - motion-vector far draw property block - owner: HectonIndirectVegetationRenderer
            _motionFarPropertyBlock = new MaterialPropertyBlock();
            // COLD ALLOC: Vector4[2] - scooter headlight world-position payload cache for compute darkness culling - owner: HectonIndirectVegetationRenderer
            _scooterHeadlightPositionsWs = new Vector4[MaxScooterHeadlights];
            // COLD ALLOC: Vector4[2] - scooter headlight direction payload cache for compute darkness culling - owner: HectonIndirectVegetationRenderer
            _scooterHeadlightDirectionsWs = new Vector4[MaxScooterHeadlights];
            // COLD ALLOC: Vector4[2] - scooter headlight color/intensity payload cache for compute darkness culling - owner: HectonIndirectVegetationRenderer
            _scooterHeadlightColors = new Vector4[MaxScooterHeadlights];
            // COLD ALLOC: Vector4[2] - scooter headlight cone payload cache for compute darkness culling - owner: HectonIndirectVegetationRenderer
            _scooterHeadlightConeData = new Vector4[MaxScooterHeadlights];
            // COLD ALLOC: ComputeBuffer[1] - near indirect arguments buffer - owner: HectonIndirectVegetationRenderer
            _nearIndirectArgsBuffer = new ComputeBuffer(1, sizeof(uint) * IndirectArgsCount, ComputeBufferType.IndirectArguments);
            // COLD ALLOC: ComputeBuffer[1] - far indirect arguments buffer - owner: HectonIndirectVegetationRenderer
            _farIndirectArgsBuffer = new ComputeBuffer(1, sizeof(uint) * IndirectArgsCount, ComputeBufferType.IndirectArguments);
            // COLD ALLOC: ComputeBuffer[1] - shadow indirect arguments buffer - owner: HectonIndirectVegetationRenderer
            _shadowIndirectArgsBuffer = new ComputeBuffer(1, sizeof(uint) * IndirectArgsCount, ComputeBufferType.IndirectArguments);
            _cullingKernelIndex = _cullingCompute.FindKernel("CullFloraInstances");
            _shadowCullingKernelIndex = _cullingCompute.FindKernel("CullFloraShadowInstances");
            CreateAuxiliaryMaterials();
            RefreshArgsBuffers();
        }

        private void OnEnable()
        {
            TryRegister();
        }

        private void OnDisable()
        {
            TryUnregister();
            _hasPreviousMotionCameraPosition = false;
            _previousMotionCamera = null;
        }

        private void OnDestroy()
        {
            TryUnregister();
            ReleaseIndirectArgsBuffers();
            ReleaseLegacyInstanceDataBuffer();
            ReleaseUploadedInstanceBuffers();
            ReleaseVisibleIndexBuffers();
            ReleaseAuxiliaryMaterials();

            if (_generatedMesh != null)
            {
                Destroy(_generatedMesh);
                _generatedMesh = null;
            }

            if (_generatedImpostorMesh != null)
            {
                Destroy(_generatedImpostorMesh);
                _generatedImpostorMesh = null;
            }
        }

        /// <summary>
        /// Binds an external source that owns both instance buffers and optional explicit bounds.
        /// </summary>
        /// <param name="bufferSource">External source that owns the GPU buffers.</param>
        public void BindSource(IHectonIndirectVegetationBufferSource bufferSource)
        {
            _bufferSource = bufferSource;
            SyncSourceBinding();
        }

        /// <summary>
        /// Clears the current external source binding.
        /// </summary>
        public void ClearSource()
        {
            _bufferSource = null;
            ClearInstanceBuffer();
            ClearDrawBoundsOverride();
        }

        /// <summary>
        /// Binds the external per-instance matrix buffer populated by another system.
        /// </summary>
        /// <param name="instanceMatrixBuffer">Structured buffer of Matrix4x4 transforms.</param>
        /// <param name="instanceCount">Active instance count contained in the buffer.</param>
        public void BindInstanceBuffer(ComputeBuffer instanceMatrixBuffer, int instanceCount)
        {
            _bufferSource = null;

            if (instanceMatrixBuffer == null || instanceCount <= 0 || instanceMatrixBuffer.count <= 0)
            {
                ClearInstanceBuffer();
                return;
            }

            _instanceMatrixBuffer = instanceMatrixBuffer;
            _matrixBindingDirty = true;
            _visibleBindingDirty = true;
            _legacyDataDirty = true;
            SetInstanceCount(instanceCount);
        }

        /// <summary>
        /// Binds the external per-instance metadata buffer populated by another system.
        /// </summary>
        /// <param name="instanceDataBuffer">Structured buffer of <see cref="HectonVegetationInstanceData"/> payloads.</param>
        public void BindInstanceDataBuffer(ComputeBuffer instanceDataBuffer)
        {
            _bufferSource = null;

            if (instanceDataBuffer == null || instanceDataBuffer.count <= 0)
            {
                ClearInstanceDataBuffer();
                return;
            }

            _instanceDataBuffer = instanceDataBuffer;
            _dataBindingDirty = true;
            _visibleBindingDirty = true;
        }

        /// <summary>
        /// Uploads caller-owned arrays into renderer-owned GPU staging buffers and binds them for indirect rendering.
        /// </summary>
        /// <param name="instanceMatrices">Caller-owned instance matrix array.</param>
        /// <param name="instanceData">Caller-owned vegetation metadata array. Pass null to use the fallback metadata path.</param>
        /// <param name="instanceCount">Number of valid entries contained in the caller arrays.</param>
        public void BindInstanceArrays(
            Matrix4x4[] instanceMatrices,
            HectonVegetationInstanceData[] instanceData,
            int instanceCount)
        {
            _bufferSource = null;

            if (instanceMatrices == null || instanceCount <= 0 || instanceMatrices.Length < instanceCount)
            {
                ClearInstanceBuffer();
                return;
            }

            EnsureUploadedInstanceBufferCapacity(instanceCount, instanceData != null);
            if (_uploadedInstanceMatrixBuffer == null)
            {
                ClearInstanceBuffer();
                return;
            }

            _uploadedInstanceMatrixBuffer.SetData(instanceMatrices, 0, 0, instanceCount);
            _instanceMatrixBuffer = _uploadedInstanceMatrixBuffer;
            _matrixBindingDirty = true;
            _visibleBindingDirty = true;

            if (instanceData != null)
            {
                if (instanceData.Length < instanceCount || _uploadedInstanceDataBuffer == null)
                {
                    ClearInstanceBuffer();
                    return;
                }

                _uploadedInstanceDataBuffer.SetData(instanceData, 0, 0, instanceCount);
                _instanceDataBuffer = _uploadedInstanceDataBuffer;
                _dataBindingDirty = true;
                _visibleBindingDirty = true;
                _legacyDataDirty = false;
            }
            else
            {
                _instanceDataBuffer = null;
                _dataBindingDirty = true;
                _visibleBindingDirty = true;
                _legacyDataDirty = true;
            }

            SetInstanceCount(instanceCount);
        }

        /// <summary>
        /// Uploads caller-owned instance matrices into renderer-owned GPU staging buffers and uses legacy metadata fallback.
        /// </summary>
        /// <param name="instanceMatrices">Caller-owned instance matrix array.</param>
        /// <param name="instanceCount">Number of valid entries contained in the caller array.</param>
        public void BindInstanceArrays(Matrix4x4[] instanceMatrices, int instanceCount)
        {
            BindInstanceArrays(instanceMatrices, null, instanceCount);
        }

        private bool BindInstanceNativeArrays(
            NativeArray<Matrix4x4> instanceMatrices,
            NativeArray<HectonVegetationInstanceData> instanceData,
            int instanceCount)
        {
            if (!instanceMatrices.IsCreated || !instanceData.IsCreated || instanceCount <= 0)
                return false;

            if (instanceMatrices.Length < instanceCount || instanceData.Length < instanceCount)
                return false;

            EnsureUploadedInstanceBufferCapacity(instanceCount, true);
            if (_uploadedInstanceMatrixBuffer == null || _uploadedInstanceDataBuffer == null)
                return false;

            _uploadedInstanceMatrixBuffer.SetData(instanceMatrices, 0, 0, instanceCount);
            _uploadedInstanceDataBuffer.SetData(instanceData, 0, 0, instanceCount);
            _instanceMatrixBuffer = _uploadedInstanceMatrixBuffer;
            _instanceDataBuffer = _uploadedInstanceDataBuffer;
            _matrixBindingDirty = true;
            _dataBindingDirty = true;
            _visibleBindingDirty = true;
            _legacyDataDirty = false;
            SetInstanceCount(instanceCount);
            return true;
        }

        /// <summary>
        /// Clears the current external instance buffer binding.
        /// </summary>
        public void ClearInstanceBuffer()
        {
            _bufferSource = null;
            ClearBoundInstanceState();
        }

        /// <summary>
        /// Clears the current external instance metadata buffer binding.
        /// </summary>
        public void ClearInstanceDataBuffer()
        {
            _bufferSource = null;
            _instanceDataBuffer = null;
            _dataBindingDirty = true;
            _legacyDataDirty = true;
        }

        /// <summary>
        /// Updates the active instance count used by the indirect args buffers.
        /// </summary>
        /// <param name="instanceCount">Number of instances to draw.</param>
        public void SetInstanceCount(int instanceCount)
        {
            int clampedCount = Mathf.Max(0, instanceCount);
            if (_instanceCount == clampedCount)
                return;

            _instanceCount = clampedCount;
            _argsDirty = true;
            _visibleBindingDirty = true;
            _legacyDataDirty = true;
        }

        /// <summary>
        /// Overrides the world-space draw bounds used by the indirect draw calls.
        /// </summary>
        /// <param name="drawBounds">Explicit world-space bounds.</param>
        public void SetDrawBounds(Bounds drawBounds)
        {
            _explicitBounds = drawBounds;
            _hasBoundsOverride = true;
        }

        /// <summary>
        /// Returns to transform-relative fallback draw bounds.
        /// </summary>
        public void ClearDrawBoundsOverride()
        {
            _hasBoundsOverride = false;
        }

        /// <summary>
        /// Executes the indirect vegetation draw calls.
        /// </summary>
        /// <param name="deltaTime">Unused current frame delta required by ITickable.</param>
        public void Tick(float deltaTime)
        {
            SyncSourceBinding();

            if (_instanceMatrixBuffer == null || _instanceCount <= 0 || _material == null)
                return;

            if (_cullingCompute == null || _cullingKernelIndex < 0)
                return;

            Mesh nearMesh = ResolveNearRenderMesh();
            if (nearMesh == null || _nearIndirectArgsBuffer == null || _nearPropertyBlock == null)
                return;

            if (!TryDispatchGpuCulling())
                return;

            if (_argsDirty)
                RefreshArgsBuffers();

            ComputeBuffer.CopyCount(_visibleIndexBufferLod0, _nearIndirectArgsBuffer, sizeof(uint));
            ComputeBuffer.CopyCount(_visibleIndexBufferLod1, _farIndirectArgsBuffer, sizeof(uint));
            if (_enableShadowCasterDraw && _visibleIndexBufferShadow != null && _shadowIndirectArgsBuffer != null)
                ComputeBuffer.CopyCount(_visibleIndexBufferShadow, _shadowIndirectArgsBuffer, sizeof(uint));

            if (!TryBindPropertyBlocks())
                return;

            Bounds drawBounds = _hasBoundsOverride
                ? _explicitBounds
                : new Bounds(transform.position + _boundsCenterOffset, _boundsSize);

            if (_enableDepthPrepass && _depthOnlyMaterial != null)
                DrawDepthPrepass(nearMesh, drawBounds);

            if (_enableShadowCasterDraw && _shadowCasterMaterial != null)
                DrawShadowPass(nearMesh, drawBounds);

            if (_enableMotionVectorDraw && _motionVectorMaterial != null)
                DrawMotionVectorPasses(nearMesh, drawBounds);

            DrawNearPass(nearMesh, drawBounds);

            if (_farLodDistance > _nearLodDistance)
            {
                Mesh farMesh = ResolveImpostorRenderMesh();
                if (farMesh != null && _farIndirectArgsBuffer != null && _farPropertyBlock != null)
                    DrawFarPass(farMesh, drawBounds);
            }
        }

        private Mesh ResolveNearRenderMesh()
        {
            return _generatedMesh != null ? _generatedMesh : _mesh;
        }

        private Mesh ResolveImpostorRenderMesh()
        {
            if (_generatedImpostorMesh != null)
                return _generatedImpostorMesh;

            if (_impostorMesh != null)
                return _impostorMesh;

            return ResolveNearRenderMesh();
        }

        private void SyncSourceBinding()
        {
            if (_bufferSource == null)
                return;

            if (_bufferSource is IHectonIndirectVegetationNativeBufferSource nativeBufferSource)
            {
                if (!nativeBufferSource.TryAcquireNativeReadBuffer(out HectonIndirectVegetationNativeReadBuffer readBuffer) ||
                    !readBuffer.IsValid)
                {
                    ClearBoundInstanceState();
                    if (_bufferSource.HasExplicitBounds)
                        SetDrawBounds(_bufferSource.DrawBounds);
                    else
                        ClearDrawBoundsOverride();
                    return;
                }

                JobHandle producerHandle = readBuffer.ProducerHandle;
                if (!producerHandle.Equals(default) && !producerHandle.IsCompleted)
                {
                    nativeBufferSource.ReleaseNativeReadBuffer(readBuffer, default);
                    return;
                }

                bool uploadSucceeded = BindInstanceNativeArrays(
                    readBuffer.InstanceMatrices,
                    readBuffer.InstanceData,
                    readBuffer.InstanceCount);

                nativeBufferSource.ReleaseNativeReadBuffer(readBuffer, default);

                if (!uploadSucceeded)
                {
                    ClearBoundInstanceState();
                    if (readBuffer.HasExplicitBounds)
                        SetDrawBounds(readBuffer.DrawBounds);
                    else
                        ClearDrawBoundsOverride();
                    return;
                }

                if (readBuffer.HasExplicitBounds)
                    SetDrawBounds(readBuffer.DrawBounds);
                else
                    ClearDrawBoundsOverride();

                return;
            }

            ComputeBuffer sourceMatrixBuffer = _bufferSource.InstanceMatrixBuffer;
            ComputeBuffer sourceDataBuffer = _bufferSource.InstanceDataBuffer;
            int sourceInstanceCount = _bufferSource.InstanceCount;

            if (sourceMatrixBuffer == null || sourceInstanceCount <= 0 || sourceMatrixBuffer.count <= 0)
            {
                ClearBoundInstanceState();
                if (_bufferSource.HasExplicitBounds)
                    SetDrawBounds(_bufferSource.DrawBounds);
                else
                    ClearDrawBoundsOverride();
                return;
            }

            if (_instanceMatrixBuffer != sourceMatrixBuffer)
            {
                _instanceMatrixBuffer = sourceMatrixBuffer;
                _matrixBindingDirty = true;
            }

            if (_instanceDataBuffer != sourceDataBuffer)
            {
                _instanceDataBuffer = sourceDataBuffer != null && sourceDataBuffer.count > 0 ? sourceDataBuffer : null;
                _dataBindingDirty = true;
                _visibleBindingDirty = true;
            }

            SetInstanceCount(sourceInstanceCount);

            if (_bufferSource.HasExplicitBounds)
                SetDrawBounds(_bufferSource.DrawBounds);
            else
                ClearDrawBoundsOverride();
        }

        private void ClearBoundInstanceState()
        {
            _instanceMatrixBuffer = null;
            _instanceDataBuffer = null;
            _instanceCount = 0;
            _argsDirty = true;
            _matrixBindingDirty = true;
            _dataBindingDirty = true;
            _visibleBindingDirty = true;
            _legacyDataDirty = true;
        }

        private bool TryBindPropertyBlocks()
        {
            if (_instanceMatrixBuffer == null || _instanceCount <= 0)
                return false;

            if (_matrixBindingDirty)
            {
                _nearPropertyBlock.SetBuffer(_InstanceMatricesId, _instanceMatrixBuffer);
                _farPropertyBlock.SetBuffer(_InstanceMatricesId, _instanceMatrixBuffer);
                _depthNearPropertyBlock.SetBuffer(_InstanceMatricesId, _instanceMatrixBuffer);
                _depthFarPropertyBlock.SetBuffer(_InstanceMatricesId, _instanceMatrixBuffer);
                _shadowPropertyBlock.SetBuffer(_InstanceMatricesId, _instanceMatrixBuffer);
                _motionNearPropertyBlock.SetBuffer(_InstanceMatricesId, _instanceMatrixBuffer);
                _motionFarPropertyBlock.SetBuffer(_InstanceMatricesId, _instanceMatrixBuffer);
                _matrixBindingDirty = false;
            }

            ComputeBuffer activeInstanceDataBuffer = ResolveActiveInstanceDataBuffer();
            if (activeInstanceDataBuffer == null)
                return false;

            if (_dataBindingDirty || _legacyDataDirty)
            {
                _nearPropertyBlock.SetBuffer(_InstanceDataId, activeInstanceDataBuffer);
                _farPropertyBlock.SetBuffer(_InstanceDataId, activeInstanceDataBuffer);
                _depthNearPropertyBlock.SetBuffer(_InstanceDataId, activeInstanceDataBuffer);
                _depthFarPropertyBlock.SetBuffer(_InstanceDataId, activeInstanceDataBuffer);
                _shadowPropertyBlock.SetBuffer(_InstanceDataId, activeInstanceDataBuffer);
                _motionNearPropertyBlock.SetBuffer(_InstanceDataId, activeInstanceDataBuffer);
                _motionFarPropertyBlock.SetBuffer(_InstanceDataId, activeInstanceDataBuffer);
                _dataBindingDirty = false;
                _legacyDataDirty = false;
            }

            if (_visibleBindingDirty)
            {
                if (_visibleIndexBufferLod0 == null || _visibleIndexBufferLod1 == null)
                    return false;

                _nearPropertyBlock.SetBuffer(_VisibleInstanceIndicesId, _visibleIndexBufferLod0);
                _farPropertyBlock.SetBuffer(_VisibleInstanceIndicesId, _visibleIndexBufferLod1);
                _depthNearPropertyBlock.SetBuffer(_VisibleInstanceIndicesId, _visibleIndexBufferLod0);
                _depthFarPropertyBlock.SetBuffer(_VisibleInstanceIndicesId, _visibleIndexBufferLod1);
                _motionNearPropertyBlock.SetBuffer(_VisibleInstanceIndicesId, _visibleIndexBufferLod0);
                _motionFarPropertyBlock.SetBuffer(_VisibleInstanceIndicesId, _visibleIndexBufferLod1);
                if (_visibleIndexBufferShadow != null)
                    _shadowPropertyBlock.SetBuffer(_VisibleInstanceIndicesId, _visibleIndexBufferShadow);
                _visibleBindingDirty = false;
            }

            Vector4 globalFloatingOffset = ResolveGlobalFloatingOffset();
            if (_lastGlobalFloatingOffset != globalFloatingOffset)
            {
                _nearPropertyBlock.SetVector(_GlobalFloatingOffsetId, globalFloatingOffset);
                _nearPropertyBlock.SetVector(_ChunkWorldOffsetId, globalFloatingOffset);
                _farPropertyBlock.SetVector(_GlobalFloatingOffsetId, globalFloatingOffset);
                _farPropertyBlock.SetVector(_ChunkWorldOffsetId, globalFloatingOffset);
                _depthNearPropertyBlock.SetVector(_GlobalFloatingOffsetId, globalFloatingOffset);
                _depthNearPropertyBlock.SetVector(_ChunkWorldOffsetId, globalFloatingOffset);
                _depthFarPropertyBlock.SetVector(_GlobalFloatingOffsetId, globalFloatingOffset);
                _depthFarPropertyBlock.SetVector(_ChunkWorldOffsetId, globalFloatingOffset);
                _shadowPropertyBlock.SetVector(_GlobalFloatingOffsetId, globalFloatingOffset);
                _shadowPropertyBlock.SetVector(_ChunkWorldOffsetId, globalFloatingOffset);
                _motionNearPropertyBlock.SetVector(_GlobalFloatingOffsetId, globalFloatingOffset);
                _motionNearPropertyBlock.SetVector(_ChunkWorldOffsetId, globalFloatingOffset);
                _motionFarPropertyBlock.SetVector(_GlobalFloatingOffsetId, globalFloatingOffset);
                _motionFarPropertyBlock.SetVector(_ChunkWorldOffsetId, globalFloatingOffset);
                _lastGlobalFloatingOffset = globalFloatingOffset;
            }

            return true;
        }

        private bool TryDispatchGpuCulling()
        {
            ComputeBuffer activeInstanceDataBuffer = ResolveActiveInstanceDataBuffer();
            if (activeInstanceDataBuffer == null)
                return false;

            Camera cullCamera = ResolveCullCamera();
            if (cullCamera == null)
                return false;

            EnsureVisibleIndexBufferCapacity(_instanceCount);
            if (_visibleIndexBufferLod0 == null || _visibleIndexBufferLod1 == null)
                return false;

            _visibleIndexBufferLod0.SetCounterValue(0u);
            _visibleIndexBufferLod1.SetCounterValue(0u);
            if (_visibleIndexBufferShadow != null)
                _visibleIndexBufferShadow.SetCounterValue(0u);

            EnsureDepthTextureMode(cullCamera);
            Matrix4x4 gpuProjection = GL.GetGPUProjectionMatrix(cullCamera.projectionMatrix, false);
            Matrix4x4 viewProjection = gpuProjection * cullCamera.worldToCameraMatrix;
            Texture cameraDepthTexture = _enableDepthOcclusion ? Shader.GetGlobalTexture(_GlobalCameraDepthTextureId) : null;
            bool canUseOcclusion = cameraDepthTexture != null;

            _cullingCompute.SetInt(_SourceInstanceCountId, _instanceCount);
            _cullingCompute.SetMatrix(_ViewProjectionId, viewProjection);
            _cullingCompute.SetMatrix(_ViewMatrixId, cullCamera.worldToCameraMatrix);
            _cullingCompute.SetVector(_CameraPositionId, cullCamera.transform.position);
            _cullingCompute.SetVector(_GlobalFloatingOffsetId, ResolveGlobalFloatingOffset());
            _cullingCompute.SetFloat(_LodNearDistanceId, _nearLodDistance);
            _cullingCompute.SetFloat(_LodFarDistanceId, _farLodDistance);
            _cullingCompute.SetFloat(_LodTransitionRangeId, _lodTransitionRange);
            _cullingCompute.SetInt(_OcclusionEnabledId, canUseOcclusion ? 1 : 0);
            _cullingCompute.SetFloat(_OcclusionDepthBiasId, Mathf.Max(0.01f, _occlusionDepthBias));
            _cullingCompute.SetVector(_OcclusionZBufferParamsId, Shader.GetGlobalVector(_GlobalZBufferParamsId));
            UploadDarknessCullingInputs();
            _cullingCompute.SetBuffer(_cullingKernelIndex, _SourceMatricesId, _instanceMatrixBuffer);
            _cullingCompute.SetBuffer(_cullingKernelIndex, _SourceDataId, activeInstanceDataBuffer);
            _cullingCompute.SetBuffer(_cullingKernelIndex, _VisibleIndicesLod0Id, _visibleIndexBufferLod0);
            _cullingCompute.SetBuffer(_cullingKernelIndex, _VisibleIndicesLod1Id, _visibleIndexBufferLod1);
            if (canUseOcclusion)
                _cullingCompute.SetTexture(_cullingKernelIndex, _CameraDepthTextureId, cameraDepthTexture);

            int groupCount = Mathf.CeilToInt(_instanceCount / (float)ThreadsPerGroup);
            _cullingCompute.Dispatch(_cullingKernelIndex, Mathf.Max(1, groupCount), 1, 1);

            bool canDispatchShadowCulling = _enableShadowCasterDraw &&
                _shadowCullingKernelIndex >= 0 &&
                _visibleIndexBufferShadow != null &&
                HasMainDirectionalShadowLight();

            if (canDispatchShadowCulling)
            {
                _cullingCompute.SetInt(_SourceInstanceCountId, _instanceCount);
                _cullingCompute.SetVector(_GlobalFloatingOffsetId, ResolveGlobalFloatingOffset());
                _cullingCompute.SetFloat(_LodNearDistanceId, _nearLodDistance);
                _cullingCompute.SetFloat(_LodFarDistanceId, _farLodDistance);
                _cullingCompute.SetFloat(_LodTransitionRangeId, _lodTransitionRange);
                _cullingCompute.SetBuffer(_shadowCullingKernelIndex, _SourceMatricesId, _instanceMatrixBuffer);
                _cullingCompute.SetBuffer(_shadowCullingKernelIndex, _SourceDataId, activeInstanceDataBuffer);
                _cullingCompute.SetBuffer(_shadowCullingKernelIndex, _VisibleIndicesShadowId, _visibleIndexBufferShadow);
                _cullingCompute.Dispatch(_shadowCullingKernelIndex, Mathf.Max(1, groupCount), 1, 1);
            }

            return true;
        }

        private void UploadDarknessCullingInputs()
        {
            _cullingCompute.SetInt(_DarknessCullEnabledId, _enableDarknessCulling ? 1 : 0);
            _cullingCompute.SetFloat(_DarknessBiolumThresholdId, Mathf.Max(0.001f, _darknessBiolumThreshold));
            _cullingCompute.SetFloat(_FloorBiolumStrengthId, Shader.GetGlobalFloat(_FloorBiolumStrengthId));
            _cullingCompute.SetFloat(_OceanBiolumStrengthId, Shader.GetGlobalFloat(_OceanBiolumStrengthId));
            _cullingCompute.SetFloat(_GlobalBiolumIntensityId, Shader.GetGlobalFloat(_GlobalBiolumIntensityId));

            int headlightCount = CopyScooterHeadlightPayload();
            _cullingCompute.SetInt(_ScooterHeadlightCountId, headlightCount);
            _cullingCompute.SetVectorArray(_ScooterHeadlightPositionsWsId, _scooterHeadlightPositionsWs);
            _cullingCompute.SetVectorArray(_ScooterHeadlightDirectionsWsId, _scooterHeadlightDirectionsWs);
            _cullingCompute.SetVectorArray(_ScooterHeadlightColorsId, _scooterHeadlightColors);
            _cullingCompute.SetVectorArray(_ScooterHeadlightConeDataId, _scooterHeadlightConeData);
        }

        private int CopyScooterHeadlightPayload()
        {
            ClearScooterHeadlightPayload();

            if (!_enableDarknessCulling)
                return 0;

            if (_playerToolManager == null)
                ResolvePlayerToolManager();

            if (_playerToolManager == null || _playerToolManager.IsSwapping)
                return 0;

            if (!(_playerToolManager.CurrentTool is MantaScooter scooter) || !scooter.IsTransportActive)
                return 0;

            return scooter.CopyHeadlightPayloadNonAlloc(
                _scooterHeadlightPositionsWs,
                _scooterHeadlightDirectionsWs,
                _scooterHeadlightColors,
                _scooterHeadlightConeData);
        }

        private void ResolvePlayerToolManager()
        {
            if (_playerToolManager != null)
                return;

            float currentTime = Time.unscaledTime;
            if (currentTime < _nextToolManagerResolveTime)
                return;

            _nextToolManagerResolveTime = currentTime + 2f;
            if (!BootstrapState.TryGetCurrentPlayerTransform(out Transform playerTransform) || playerTransform == null)
                return;

            _playerToolManager = playerTransform.GetComponentInChildren<PlayerToolManager>(true);
        }

        private void ClearScooterHeadlightPayload()
        {
            if (_scooterHeadlightPositionsWs == null ||
                _scooterHeadlightDirectionsWs == null ||
                _scooterHeadlightColors == null ||
                _scooterHeadlightConeData == null)
            {
                return;
            }

            for (int headlightIndex = 0; headlightIndex < MaxScooterHeadlights; headlightIndex++)
            {
                _scooterHeadlightPositionsWs[headlightIndex] = Vector4.zero;
                _scooterHeadlightDirectionsWs[headlightIndex] = Vector4.zero;
                _scooterHeadlightColors[headlightIndex] = Vector4.zero;
                _scooterHeadlightConeData[headlightIndex] = Vector4.zero;
            }
        }

        private static Vector4 ResolveGlobalFloatingOffset()
        {
            Vector3 totalOffset = HectonMapMagicVegetationBridge.GlobalTotalUniverseOffset;
            return new Vector4(totalOffset.x, totalOffset.y, totalOffset.z, 0f);
        }

        private static void EnsureDepthTextureMode(Camera targetCamera)
        {
            if (targetCamera == null)
                return;

            DepthTextureMode requiredModes = DepthTextureMode.Depth | DepthTextureMode.MotionVectors;
            if ((targetCamera.depthTextureMode & requiredModes) != requiredModes)
                targetCamera.depthTextureMode |= requiredModes;
        }

        private ComputeBuffer ResolveActiveInstanceDataBuffer()
        {
            if (_instanceDataBuffer != null)
                return _instanceDataBuffer;

            if (_instanceCount <= 0)
                return null;

            EnsureLegacyInstanceDataCapacity(_instanceCount);
            if (_legacyInstanceDataBuffer == null || _legacyInstanceData == null)
                return null;

            if (_legacyDataDirty)
            {
                FillLegacyInstanceData(_instanceCount);
                _legacyInstanceDataBuffer.SetData(_legacyInstanceData, 0, 0, _instanceCount);
            }

            return _legacyInstanceDataBuffer;
        }

        private void EnsureLegacyInstanceDataCapacity(int instanceCount)
        {
            if (instanceCount <= 0)
                return;

            if (_legacyInstanceData != null &&
                _legacyInstanceData.Length >= instanceCount &&
                _legacyInstanceDataBuffer != null &&
                _legacyInstanceDataBuffer.count >= instanceCount)
            {
                return;
            }

            int nextCapacity = Mathf.NextPowerOfTwo(Mathf.Max(16, instanceCount));

            ReleaseLegacyInstanceDataBuffer();

            // COLD ALLOC: HectonVegetationInstanceData[nextCapacity] - legacy metadata fallback staging - owner: HectonIndirectVegetationRenderer
            _legacyInstanceData = new HectonVegetationInstanceData[nextCapacity];
            // COLD ALLOC: ComputeBuffer[nextCapacity] - legacy instance metadata fallback buffer - owner: HectonIndirectVegetationRenderer
            _legacyInstanceDataBuffer = new ComputeBuffer(nextCapacity, InstanceDataStride, ComputeBufferType.Structured);
            _legacyDataDirty = true;
            _dataBindingDirty = true;
        }

        private void EnsureUploadedInstanceBufferCapacity(int instanceCount, bool requiresInstanceDataBuffer)
        {
            if (instanceCount <= 0)
                return;

            if (_uploadedInstanceMatrixBuffer == null || _uploadedInstanceMatrixBuffer.count < instanceCount)
            {
                if (_uploadedInstanceMatrixBuffer != null)
                {
                    _uploadedInstanceMatrixBuffer.Release();
                    _uploadedInstanceMatrixBuffer = null;
                }

                int nextCapacity = Mathf.NextPowerOfTwo(Mathf.Max(16, instanceCount));
                // COLD ALLOC: ComputeBuffer[nextCapacity] - owned matrix upload staging buffer - owner: HectonIndirectVegetationRenderer
                _uploadedInstanceMatrixBuffer = new ComputeBuffer(nextCapacity, InstanceMatrixStride, ComputeBufferType.Structured);
            }

            if (!requiresInstanceDataBuffer)
                return;

            if (_uploadedInstanceDataBuffer == null || _uploadedInstanceDataBuffer.count < instanceCount)
            {
                if (_uploadedInstanceDataBuffer != null)
                {
                    _uploadedInstanceDataBuffer.Release();
                    _uploadedInstanceDataBuffer = null;
                }

                int nextCapacity = Mathf.NextPowerOfTwo(Mathf.Max(16, instanceCount));
                // COLD ALLOC: ComputeBuffer[nextCapacity] - owned metadata upload staging buffer - owner: HectonIndirectVegetationRenderer
                _uploadedInstanceDataBuffer = new ComputeBuffer(nextCapacity, InstanceDataStride, ComputeBufferType.Structured);
            }
        }

        private void EnsureVisibleIndexBufferCapacity(int instanceCount)
        {
            if (instanceCount <= 0)
                return;

            int nextCapacity = Mathf.NextPowerOfTwo(Mathf.Max(16, instanceCount));

            if (_visibleIndexBufferLod0 == null || _visibleIndexBufferLod0.count < instanceCount)
            {
                if (_visibleIndexBufferLod0 != null)
                    _visibleIndexBufferLod0.Release();

                // COLD ALLOC: ComputeBuffer[nextCapacity] - visible LOD0 append index buffer - owner: HectonIndirectVegetationRenderer
                _visibleIndexBufferLod0 = new ComputeBuffer(nextCapacity, VisibleIndexStride, ComputeBufferType.Append);
                _visibleBindingDirty = true;
            }

            if (_visibleIndexBufferLod1 == null || _visibleIndexBufferLod1.count < instanceCount)
            {
                if (_visibleIndexBufferLod1 != null)
                    _visibleIndexBufferLod1.Release();

                // COLD ALLOC: ComputeBuffer[nextCapacity] - visible LOD1 append index buffer - owner: HectonIndirectVegetationRenderer
                _visibleIndexBufferLod1 = new ComputeBuffer(nextCapacity, VisibleIndexStride, ComputeBufferType.Append);
                _visibleBindingDirty = true;
            }

            if (_visibleIndexBufferShadow == null || _visibleIndexBufferShadow.count < instanceCount)
            {
                if (_visibleIndexBufferShadow != null)
                    _visibleIndexBufferShadow.Release();

                // COLD ALLOC: ComputeBuffer[nextCapacity] - visible shadow append index buffer - owner: HectonIndirectVegetationRenderer
                _visibleIndexBufferShadow = new ComputeBuffer(nextCapacity, VisibleIndexStride, ComputeBufferType.Append);
                _visibleBindingDirty = true;
            }
        }

        private void FillLegacyInstanceData(int instanceCount)
        {
            HectonVegetationInstanceData defaultPayload = CreateLegacyDefaultPayload();
            for (int i = 0; i < instanceCount; i++)
                _legacyInstanceData[i] = defaultPayload;
        }

        private HectonVegetationInstanceData CreateLegacyDefaultPayload()
        {
            switch (_legacyFallbackType)
            {
                case HectonVegetationInstanceType.GiantKelp:
                    return new HectonVegetationInstanceData(HectonVegetationInstanceType.GiantKelp, 0.55f, 0.8f, 0.5f);
                case HectonVegetationInstanceType.Sargassum:
                    return new HectonVegetationInstanceData(HectonVegetationInstanceType.Sargassum, 0.4f, 0.9f, 0.5f);
                default:
                    return new HectonVegetationInstanceData(HectonVegetationInstanceType.Grass, 0.55f, 1f, 0.5f);
            }
        }

        private void ReleaseLegacyInstanceDataBuffer()
        {
            if (_legacyInstanceDataBuffer != null)
            {
                _legacyInstanceDataBuffer.Release();
                _legacyInstanceDataBuffer = null;
            }

            _legacyInstanceData = null;
        }

        private void ReleaseUploadedInstanceBuffers()
        {
            if (_uploadedInstanceMatrixBuffer != null)
            {
                _uploadedInstanceMatrixBuffer.Release();
                _uploadedInstanceMatrixBuffer = null;
            }

            if (_uploadedInstanceDataBuffer != null)
            {
                _uploadedInstanceDataBuffer.Release();
                _uploadedInstanceDataBuffer = null;
            }
        }

        private void ReleaseVisibleIndexBuffers()
        {
            if (_visibleIndexBufferLod0 != null)
            {
                _visibleIndexBufferLod0.Release();
                _visibleIndexBufferLod0 = null;
            }

            if (_visibleIndexBufferLod1 != null)
            {
                _visibleIndexBufferLod1.Release();
                _visibleIndexBufferLod1 = null;
            }

            if (_visibleIndexBufferShadow != null)
            {
                _visibleIndexBufferShadow.Release();
                _visibleIndexBufferShadow = null;
            }
        }

        private void ReleaseIndirectArgsBuffers()
        {
            if (_nearIndirectArgsBuffer != null)
            {
                _nearIndirectArgsBuffer.Release();
                _nearIndirectArgsBuffer = null;
            }

            if (_farIndirectArgsBuffer != null)
            {
                _farIndirectArgsBuffer.Release();
                _farIndirectArgsBuffer = null;
            }

            if (_shadowIndirectArgsBuffer != null)
            {
                _shadowIndirectArgsBuffer.Release();
                _shadowIndirectArgsBuffer = null;
            }
        }

        private void RefreshArgsBuffers()
        {
            RefreshArgsBuffer(ResolveNearRenderMesh(), _nearIndirectArgs, _nearIndirectArgsBuffer);
            RefreshArgsBuffer(ResolveImpostorRenderMesh(), _farIndirectArgs, _farIndirectArgsBuffer);
            RefreshArgsBuffer(ResolveNearRenderMesh(), _nearIndirectArgs, _shadowIndirectArgsBuffer);
            _argsDirty = false;
        }

        private void RefreshArgsBuffer(Mesh renderMesh, uint[] args, ComputeBuffer argsBuffer)
        {
            if (renderMesh == null || argsBuffer == null)
                return;

            int subMeshIndex = Mathf.Clamp(_subMeshIndex, 0, renderMesh.subMeshCount - 1);
            args[0] = (uint)renderMesh.GetIndexCount(subMeshIndex);
            args[1] = 0u;
            args[2] = (uint)renderMesh.GetIndexStart(subMeshIndex);
            args[3] = (uint)renderMesh.GetBaseVertex(subMeshIndex);
            args[4] = 0u;
            argsBuffer.SetData(args);
        }

        private Camera ResolveCullCamera()
        {
            if (_cameraOverride != null && _cameraOverride.isActiveAndEnabled)
            {
                _cachedCullCamera = _cameraOverride;
                return _cachedCullCamera;
            }

            if (_cachedCullCamera != null && _cachedCullCamera.isActiveAndEnabled)
                return _cachedCullCamera;

            int cameraCount = Mathf.Min(Camera.allCamerasCount, _cameraSearchCache.Length);
            if (cameraCount <= 0)
                return null;

            Camera.GetAllCameras(_cameraSearchCache);

            Camera fallbackCamera = null;
            for (int i = 0; i < cameraCount; i++)
            {
                Camera candidate = _cameraSearchCache[i];
                if (candidate == null || !candidate.isActiveAndEnabled)
                    continue;

                if (fallbackCamera == null)
                    fallbackCamera = candidate;

                if (candidate.CompareTag("MainCamera"))
                {
                    _cachedCullCamera = candidate;
                    return _cachedCullCamera;
                }
            }

            _cachedCullCamera = fallbackCamera;
            return _cachedCullCamera;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            TryAutoAssignAssets();
        }

        private void TryAutoAssignAssets()
        {
            if (_cullingCompute == null)
                _cullingCompute = AssetDatabase.LoadAssetAtPath<ComputeShader>(ComputeShaderAssetPath);

            if (_depthOnlyShader == null)
                _depthOnlyShader = AssetDatabase.LoadAssetAtPath<Shader>(DepthShaderAssetPath);

            if (_shadowCasterShader == null)
                _shadowCasterShader = AssetDatabase.LoadAssetAtPath<Shader>(ShadowShaderAssetPath);

            if (_motionVectorShader == null)
                _motionVectorShader = AssetDatabase.LoadAssetAtPath<Shader>(MotionShaderAssetPath);
        }
#endif

        private void CreateAuxiliaryMaterials()
        {
            if (_enableDepthPrepass && _depthOnlyMaterial == null && _depthOnlyShader != null)
            {
                _depthOnlyMaterial = new Material(_depthOnlyShader)
                {
                    hideFlags = HideFlags.HideAndDontSave
                }; // COLD ALLOC: Material[1] - dedicated depth-only indirect vegetation material - owner: HectonIndirectVegetationRenderer
            }

            if (_enableShadowCasterDraw && _shadowCasterMaterial == null && _shadowCasterShader != null)
            {
                _shadowCasterMaterial = new Material(_shadowCasterShader)
                {
                    hideFlags = HideFlags.HideAndDontSave
                }; // COLD ALLOC: Material[1] - dedicated shadow-only indirect vegetation material - owner: HectonIndirectVegetationRenderer
            }

            if (_enableMotionVectorDraw && _motionVectorMaterial == null && _motionVectorShader != null)
            {
                _motionVectorMaterial = new Material(_motionVectorShader)
                {
                    hideFlags = HideFlags.HideAndDontSave
                }; // COLD ALLOC: Material[1] - dedicated motion-vector indirect vegetation material - owner: HectonIndirectVegetationRenderer
            }
        }

        private void ReleaseAuxiliaryMaterials()
        {
            if (_depthOnlyMaterial != null)
            {
                Destroy(_depthOnlyMaterial);
                _depthOnlyMaterial = null;
            }

            if (_shadowCasterMaterial != null)
            {
                Destroy(_shadowCasterMaterial);
                _shadowCasterMaterial = null;
            }

            if (_motionVectorMaterial != null)
            {
                Destroy(_motionVectorMaterial);
                _motionVectorMaterial = null;
            }
        }

        private void DrawNearPass(Mesh renderMesh, Bounds drawBounds)
        {
            ConfigurePassPropertyBlock(_nearPropertyBlock, 0f);
            int subMeshIndex = Mathf.Clamp(_subMeshIndex, 0, renderMesh.subMeshCount - 1);
            Camera renderCamera = _cameraOverride != null ? _cameraOverride : _cachedCullCamera;
            ShadowCastingMode shadowMode = _enableShadowCasterDraw ? ShadowCastingMode.Off : _shadowCastingMode;

#pragma warning disable CS0618
            Graphics.DrawMeshInstancedIndirect(
                renderMesh,
                subMeshIndex,
                _material,
                drawBounds,
                _nearIndirectArgsBuffer,
                0,
                _nearPropertyBlock,
                shadowMode,
                _receiveShadows,
                gameObject.layer,
                renderCamera,
                LightProbeUsage.Off);
#pragma warning restore CS0618
        }

        private void DrawFarPass(Mesh renderMesh, Bounds drawBounds)
        {
            ConfigurePassPropertyBlock(_farPropertyBlock, 1f);
            int subMeshIndex = Mathf.Clamp(_subMeshIndex, 0, renderMesh.subMeshCount - 1);
            Camera renderCamera = _cameraOverride != null ? _cameraOverride : _cachedCullCamera;
            ShadowCastingMode shadowMode = _enableShadowCasterDraw ? ShadowCastingMode.Off : _impostorShadowCastingMode;

#pragma warning disable CS0618
            Graphics.DrawMeshInstancedIndirect(
                renderMesh,
                subMeshIndex,
                _material,
                drawBounds,
                _farIndirectArgsBuffer,
                0,
                _farPropertyBlock,
                shadowMode,
                _impostorReceiveShadows,
                gameObject.layer,
                renderCamera,
                LightProbeUsage.Off);
#pragma warning restore CS0618
        }

        private void DrawDepthPrepass(Mesh nearMesh, Bounds drawBounds)
        {
            if (_depthOnlyMaterial == null || _depthNearPropertyBlock == null || _nearIndirectArgsBuffer == null)
                return;

            Camera renderCamera = _cameraOverride != null ? _cameraOverride : _cachedCullCamera;
            if (renderCamera == null)
                return;

            ConfigurePassPropertyBlock(_depthNearPropertyBlock, 0f);
            int nearSubMeshIndex = Mathf.Clamp(_subMeshIndex, 0, nearMesh.subMeshCount - 1);

#pragma warning disable CS0618
            Graphics.DrawMeshInstancedIndirect(
                nearMesh,
                nearSubMeshIndex,
                _depthOnlyMaterial,
                drawBounds,
                _nearIndirectArgsBuffer,
                0,
                _depthNearPropertyBlock,
                ShadowCastingMode.Off,
                false,
                gameObject.layer,
                renderCamera,
                LightProbeUsage.Off);
#pragma warning restore CS0618

            if (_farLodDistance <= _nearLodDistance)
                return;

            Mesh farMesh = ResolveImpostorRenderMesh();
            if (farMesh == null || _farIndirectArgsBuffer == null || _depthFarPropertyBlock == null)
                return;

            ConfigurePassPropertyBlock(_depthFarPropertyBlock, 1f);
            int farSubMeshIndex = Mathf.Clamp(_subMeshIndex, 0, farMesh.subMeshCount - 1);

#pragma warning disable CS0618
            Graphics.DrawMeshInstancedIndirect(
                farMesh,
                farSubMeshIndex,
                _depthOnlyMaterial,
                drawBounds,
                _farIndirectArgsBuffer,
                0,
                _depthFarPropertyBlock,
                ShadowCastingMode.Off,
                false,
                gameObject.layer,
                renderCamera,
                LightProbeUsage.Off);
#pragma warning restore CS0618
        }

        private void DrawShadowPass(Mesh renderMesh, Bounds drawBounds)
        {
            if (_shadowCasterMaterial == null || _shadowIndirectArgsBuffer == null || _shadowPropertyBlock == null)
                return;

            if (!HasMainDirectionalShadowLight())
                return;

            ConfigurePassPropertyBlock(_shadowPropertyBlock, 0f);
            int subMeshIndex = Mathf.Clamp(_subMeshIndex, 0, renderMesh.subMeshCount - 1);
            Camera renderCamera = _cameraOverride != null ? _cameraOverride : _cachedCullCamera;

#pragma warning disable CS0618
            Graphics.DrawMeshInstancedIndirect(
                renderMesh,
                subMeshIndex,
                _shadowCasterMaterial,
                drawBounds,
                _shadowIndirectArgsBuffer,
                0,
                _shadowPropertyBlock,
                ShadowCastingMode.On,
                false,
                gameObject.layer,
                renderCamera,
                LightProbeUsage.Off);
#pragma warning restore CS0618
        }

        private void DrawMotionVectorPasses(Mesh nearMesh, Bounds drawBounds)
        {
            if (_motionVectorMaterial == null)
                return;

            Camera renderCamera = _cameraOverride != null ? _cameraOverride : _cachedCullCamera;
            if (renderCamera == null)
                return;

            Vector3 currentCameraPosition = renderCamera.transform.position;
            Vector3 previousCameraPosition = _hasPreviousMotionCameraPosition && _previousMotionCamera == renderCamera
                ? _previousMotionCameraPosition
                : currentCameraPosition;

            _motionNearPropertyBlock.SetVector(_PreviousCameraPositionId, previousCameraPosition);
            _motionFarPropertyBlock.SetVector(_PreviousCameraPositionId, previousCameraPosition);

            ConfigurePassPropertyBlock(_motionNearPropertyBlock, 0f);
            int nearSubMeshIndex = Mathf.Clamp(_subMeshIndex, 0, nearMesh.subMeshCount - 1);

#pragma warning disable CS0618
            Graphics.DrawMeshInstancedIndirect(
                nearMesh,
                nearSubMeshIndex,
                _motionVectorMaterial,
                drawBounds,
                _nearIndirectArgsBuffer,
                0,
                _motionNearPropertyBlock,
                ShadowCastingMode.Off,
                false,
                gameObject.layer,
                renderCamera,
                LightProbeUsage.Off);
#pragma warning restore CS0618

            if (_farLodDistance <= _nearLodDistance)
            {
                _previousMotionCameraPosition = currentCameraPosition;
                _previousMotionCamera = renderCamera;
                _hasPreviousMotionCameraPosition = true;
                return;
            }

            Mesh farMesh = ResolveImpostorRenderMesh();
            if (farMesh == null || _farIndirectArgsBuffer == null || _motionFarPropertyBlock == null)
            {
                _previousMotionCameraPosition = currentCameraPosition;
                _previousMotionCamera = renderCamera;
                _hasPreviousMotionCameraPosition = true;
                return;
            }

            ConfigurePassPropertyBlock(_motionFarPropertyBlock, 1f);
            int farSubMeshIndex = Mathf.Clamp(_subMeshIndex, 0, farMesh.subMeshCount - 1);

#pragma warning disable CS0618
            Graphics.DrawMeshInstancedIndirect(
                farMesh,
                farSubMeshIndex,
                _motionVectorMaterial,
                drawBounds,
                _farIndirectArgsBuffer,
                0,
                _motionFarPropertyBlock,
                ShadowCastingMode.Off,
                false,
                gameObject.layer,
                renderCamera,
                LightProbeUsage.Off);
#pragma warning restore CS0618

            _previousMotionCameraPosition = currentCameraPosition;
            _previousMotionCamera = renderCamera;
            _hasPreviousMotionCameraPosition = true;
        }

        private static bool HasMainDirectionalShadowLight()
        {
            Light sun = RenderSettings.sun;
            return sun != null && sun.enabled && sun.type == LightType.Directional && sun.shadows != LightShadows.None;
        }

        private static long EstimateComputeBufferBytes(ComputeBuffer buffer)
        {
            return buffer != null ? (long)buffer.count * buffer.stride : 0L;
        }

        private void ConfigurePassPropertyBlock(MaterialPropertyBlock propertyBlock, float passMode)
        {
            propertyBlock.SetFloat(_LodPassModeId, passMode);
            propertyBlock.SetFloat(_LodNearDistanceId, _nearLodDistance);
            propertyBlock.SetFloat(_LodFarDistanceId, _farLodDistance);
            propertyBlock.SetFloat(_LodTransitionRangeId, _lodTransitionRange);
            propertyBlock.SetFloat(_ImpostorWidthId, _impostorWidth);
            propertyBlock.SetFloat(_ImpostorHeightId, _impostorHeight);
        }

        private static Mesh BuildImpostorCardMesh()
        {
            Mesh mesh = new Mesh
            {
                name = $"{nameof(HectonIndirectVegetationRenderer)}_ImpostorCard"
            };

            // COLD ALLOC: Vector3[4] - unit impostor card vertices - owner: HectonIndirectVegetationRenderer
            Vector3[] vertices =
            {
                new Vector3(-0.5f, 0f, 0f),
                new Vector3(-0.5f, 1f, 0f),
                new Vector3(0.5f, 1f, 0f),
                new Vector3(0.5f, 0f, 0f)
            };
            // COLD ALLOC: Vector3[4] - unit impostor card normals - owner: HectonIndirectVegetationRenderer
            Vector3[] normals =
            {
                Vector3.forward,
                Vector3.forward,
                Vector3.forward,
                Vector3.forward
            };
            // COLD ALLOC: Vector4[4] - unit impostor card tangents - owner: HectonIndirectVegetationRenderer
            Vector4[] tangents =
            {
                new Vector4(1f, 0f, 0f, 1f),
                new Vector4(1f, 0f, 0f, 1f),
                new Vector4(1f, 0f, 0f, 1f),
                new Vector4(1f, 0f, 0f, 1f)
            };
            // COLD ALLOC: Vector2[4] - unit impostor card UVs - owner: HectonIndirectVegetationRenderer
            Vector2[] uvs =
            {
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 0f)
            };
            // COLD ALLOC: int[6] - unit impostor card indices - owner: HectonIndirectVegetationRenderer
            int[] triangles = { 0, 1, 2, 0, 2, 3 };

            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.tangents = tangents;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.bounds = new Bounds(new Vector3(0f, 0.5f, 0f), new Vector3(1f, 1f, 0.01f));
            return mesh;
        }

        private void TryRegister()
        {
            if (_isRegistered)
                return;

            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager == null)
                return;

            tickManager.Register(this);
            _isRegistered = true;
        }

        private void TryUnregister()
        {
            if (!_isRegistered)
                return;

            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager != null)
                tickManager.Unregister(this);

            _isRegistered = false;
        }
    }
}
