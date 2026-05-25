using System;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Data;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.World
{
    /// <summary>
    /// Generates and renders seabed scatter entirely on the GPU from the active MapMagic height payload.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GPUScatterDirector : MonoBehaviour, ILateFrameTickable, IOriginShiftListener, IGlobalRegistryHotSwapListener
    {
        private const int ThreadGroupSize = 64;
        private const int FrustumPlaneCount = 6;
        private const int VisibleCountReadbackFrameStride = 60;
        private const int IndirectArgsInstanceCountIndex = 1;
        private const int ScatterBoundsLutCount = 16;
        private const int SargassumDensityBinCount = 64;
        private const int BiomeHeatmapResolution = 256;
        private const int BiomeHeatmapPixelCount = BiomeHeatmapResolution * BiomeHeatmapResolution;
        private const int ScatterTelemetryCapacity = 300;
        private const float SargassumDensityEncodeScale = 64f;
        private const string ScatterBlackBoxDumpRelativePath = "Docs/AgentLogs/Dump_WORLD_BIOME_BLENDING.bin";
        private const uint ScatterTelemetryHashSeed = 2166136261u;
        private const uint ScatterTelemetryMissingDependencyFlag = 1u << 0;
        private const uint ScatterTelemetryInvalidStateFlag = 1u << 1;
        private const uint ScatterTelemetryOriginShiftFlag = 1u << 2;
        private const SystemID VaultOwnerSystemId = SystemID.GraphicsScalability;
        private const BufferID ScatterTelemetryRingBufferId = BufferID.GpuScatterTelemetryRing;
        private const float ScatterMinimumNormalY = 0.70710678f;
        private const float MicroScatterLowCullMeters = 15f;
        private const float MicroScatterMidCullMeters = 22f;
        private const float MicroScatterHighCullMeters = 30f;
        private const int MicroScatterLowBudget = 8192;
        private const int MicroScatterMx350Budget = 12000;
        private const int MicroScatterMidBudget = 24000;
        private const int MicroScatterHighBudget = 50000;
#if UNITY_EDITOR
        private const string ScatterComputeAssetPath = "Assets/_Project/Art/Shaders/Hecton_GpuScatter.compute";
        private const string DepthPyramidComputeAssetPath = "Assets/_Project/Art/Shaders/Hecton_DepthPyramid.compute";
#endif
        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct ScatterTelemetryEntry
        {
            [FieldOffset(0)]
            public uint Frame;
            [FieldOffset(4)]
            public uint Flags;
            [FieldOffset(8)]
            public float3 Center;
            [FieldOffset(20)]
            public float2 AupOffsetXZ;
            [FieldOffset(28)]
            public float RadiusMeters;
            [FieldOffset(32)]
            public float CellSizeMeters;
            [FieldOffset(36)]
            public int GridResolution;
            [FieldOffset(40)]
            public int CandidateCount;
            [FieldOffset(44)]
            public uint BiomeHash;
            [FieldOffset(48)]
            public uint VisibleCount;
            [FieldOffset(52)]
            public uint StateHash;
            [FieldOffset(56)]
            public uint OriginShiftSequence;
            [FieldOffset(60)]
            public uint BlobChecksumLo;
        }

        private static readonly int _ScatterInstancesId = Shader.PropertyToID("_HectonScatterInstances");
        private static readonly int _VisibleIndicesId = Shader.PropertyToID("_HectonVisibleScatterIndices");
        private static readonly int _VisibilityCacheId = Shader.PropertyToID("_HectonScatterVisibilityCache");
        private static readonly int _ScatterDensityBinsId = Shader.PropertyToID("_HectonScatterDensityBins");
        private static readonly int _ScatterDensityBinCountId = Shader.PropertyToID("_HectonScatterDensityBinCount");
        private static readonly int _ScatterDensityParamsId = Shader.PropertyToID("_HectonScatterDensityParams");
        private static readonly int _ScatterBoundsLutId = Shader.PropertyToID("_HectonScatterBoundsLut");
        private static readonly int _ScatterBoundsLutCountId = Shader.PropertyToID("_HectonScatterBoundsLutCount");
        private static readonly int _HeightTextureId = Shader.PropertyToID("_HectonScatterHeightTexture");
        private static readonly int _HeightMaxPixelId = Shader.PropertyToID("_HectonScatterHeightMaxPixel");
        private static readonly int _HeightResolutionMinusOneId = Shader.PropertyToID("_HectonScatterHeightResolutionMinusOne");
        private static readonly int _HeightTexelSizeId = Shader.PropertyToID("_HectonScatterHeightTexelSize");
        private static readonly int _TerrainPositionId = Shader.PropertyToID("_HectonScatterTerrainPosition");
        private static readonly int _TerrainSizeId = Shader.PropertyToID("_HectonScatterTerrainSize");
        private static readonly int _TerrainSizeInvXZId = Shader.PropertyToID("_HectonScatterTerrainSizeInvXZ");
        private static readonly int _FieldRectId = Shader.PropertyToID("_HectonScatterFieldRect");
        private static readonly int _GridResolutionId = Shader.PropertyToID("_HectonScatterGridResolution");
        private static readonly int _CandidateCountId = Shader.PropertyToID("_HectonScatterCandidateCount");
        private static readonly int _CellSizeId = Shader.PropertyToID("_HectonScatterCellSize");
        private static readonly int _ScatterSeedId = Shader.PropertyToID("_HectonScatterSeed");
        private static readonly int _ScaleRangeId = Shader.PropertyToID("_HectonScatterScaleRange");
        private static readonly int _MinNormalYSqId = Shader.PropertyToID("_HectonScatterMinNormalYSq");
        private static readonly int _CameraPositionId = Shader.PropertyToID("_HectonScatterCameraPosition");
        private static readonly int _CameraForwardId = Shader.PropertyToID("_HectonScatterCameraForward");
        private static readonly int _MaxDistanceSqId = Shader.PropertyToID("_HectonScatterMaxDistanceSq");
        private static readonly int _PeripheralDistanceSqId = Shader.PropertyToID("_HectonScatterPeripheralDistanceSq");
        private static readonly int _PeripheralDotId = Shader.PropertyToID("_HectonScatterPeripheralDot");
        private static readonly int _ViewProjectionId = Shader.PropertyToID("_HectonScatterViewProjection");
        private static readonly int _ViewMatrixId = Shader.PropertyToID("_HectonScatterViewMatrix");
        private static readonly int _ScreenParamsId = Shader.PropertyToID("_HectonScatterScreenParams");
        private static readonly int _FoveatedParamsId = Shader.PropertyToID("_HectonScatterFoveatedParams");
        private static readonly int _DitherParamsId = Shader.PropertyToID("_HectonScatterDitherParams");
        private static readonly int _ScatterDepthPyramidId = Shader.PropertyToID("_HectonScatterDepthPyramid");
        private static readonly int _ScatterDepthPyramidMipCountId = Shader.PropertyToID("_HectonScatterDepthPyramidMipCount");
        private static readonly int _ScatterDepthPyramidTexelSizeId = Shader.PropertyToID("_HectonScatterDepthPyramidTexelSize");
        private static readonly int _ScatterZBufferParamsId = Shader.PropertyToID("_HectonScatterZBufferParams");
        private static readonly int _ScatterOcclusionEnabledId = Shader.PropertyToID("_HectonScatterOcclusionEnabled");
        private static readonly int _ScatterOcclusionDepthBiasId = Shader.PropertyToID("_HectonScatterOcclusionDepthBias");
        private static readonly int _ScatterFrustumPaddingId = Shader.PropertyToID("_HectonScatterFrustumPadding");
        private static readonly int _ScatterFrameQuadrantId = Shader.PropertyToID("_HectonScatterFrameQuadrant");
        private static readonly int _BiomeHeatmapTexId = Shader.PropertyToID("_HectonBiomeHeatmapTex");
        private static readonly int _BiomeGroundArrayId = Shader.PropertyToID("_HectonBiomeGroundArray");
        private static readonly int _BiomeHeatmapRectId = Shader.PropertyToID("_HectonBiomeHeatmapRect");
        private static readonly int _BiomeTextureParamsId = Shader.PropertyToID("_HectonBiomeTextureParams");
        private static readonly int _ScatterBiomeParamsId = Shader.PropertyToID("_HectonScatterBiomeParams");
        private static readonly int _ScatterAupGridOffsetId = Shader.PropertyToID("_HectonScatterAupGridOffset");
        private static readonly int _CurrentBiomeColorId = Shader.PropertyToID("_CurrentBiomeColor");
        private static readonly int _CurrentBiomeColorPlainId = Shader.PropertyToID("CurrentBiomeColor");
        private static readonly int _DepthPyramidSourceDepthId = Shader.PropertyToID("_HectonDepthPyramidSourceDepth");
        private static readonly int _DepthPyramidSourceId = Shader.PropertyToID("_HectonDepthPyramidSource");
        private static readonly int _DepthPyramidTargetId = Shader.PropertyToID("_HectonDepthPyramidTarget");
        private static readonly int _GlobalCameraDepthTextureId = Shader.PropertyToID("_CameraDepthTexture");
        private static readonly int _GlobalZBufferParamsId = Shader.PropertyToID("_ZBufferParams");
        private static readonly int _FrustumPlanesId = Shader.PropertyToID("_HectonScatterFrustumPlanes");
        private static readonly int _ModInstanceMatricesId = Shader.PropertyToID("_HectonModInstanceMatrices");
        private static readonly int _ModInstanceCountId = Shader.PropertyToID("_HectonModInstanceCount");
        private const int MaxModInstancesPerFrame = 1024;

#pragma warning disable 0649 // GPU buffer layout payload; compute/render code writes these fields outside managed C# assignment.
        private struct ScatterInstanceGpuData
        {
            public Vector4 PositionScale;
            public Vector4 NormalRotation;
            public Vector4 AtlasFlow;
        }
#pragma warning restore 0649

        private static GPUScatterDirector _activeInstance;

        [Header("References")]
        [SerializeField]
        [Tooltip("Authoritative vegetation bridge that owns the active MapMagic height payload.")]
        private HectonMapMagicVegetationBridge vegetationBridge;

        [SerializeField]
        [Tooltip("Compute shader that generates the GPU-resident scatter placement stream.")]
        private ComputeShader scatterCompute;

        [SerializeField]
        [Tooltip("Compute shader used to build the previous-frame Hi-Z depth pyramid for optional scatter occlusion.")]
        private ComputeShader depthPyramidCompute;

        [SerializeField]
        [Tooltip("Shared authored material for the indirect scatter draw. Per-instance payload is supplied through global graphics buffers.")]
        private Material scatterMaterial;

        [SerializeField]
        [Tooltip("Mesh rendered for each generated scatter instance.")]
        private Mesh scatterMesh;

        [SerializeField]
        [Tooltip("Optional camera override. When empty, the active player camera is resolved from the runtime context.")]
        private Camera viewCamera;

        [SerializeField]
        [Tooltip("Optional player transform override used to center the scatter field.")]
        private Transform playerTransform;

        [Header("Scatter Field")]
        [SerializeField, Range(12f, 80f)]
        [Tooltip("Radius in meters of the player-centered seabed scatter field.")]
        private float scatterRadiusMeters = 42f;

        [SerializeField, Range(0.25f, 4f)]
        [Tooltip("Cell size in meters used by the spatial hash placement grid.")]
        private float cellSizeMeters = 0.38f;

        [SerializeField, Range(0.15f, 2.5f)]
        [Tooltip("Minimum authored scale applied to generated scatter instances.")]
        private float minScale = 0.42f;

        [SerializeField, Range(0.15f, 3f)]
        [Tooltip("Maximum authored scale applied to generated scatter instances.")]
        private float maxScale = 1.18f;

        [SerializeField, Range(ScatterMinimumNormalY, 1f)]
        [Tooltip("Minimum terrain normal Y accepted for a generated seabed instance. Runtime never drops below the 45-degree slope gate.")]
        private float minimumNormalY = ScatterMinimumNormalY;

        [SerializeField, Min(256)]
        [Tooltip("Hard cap for generated scatter instances in the player field.")]
        private int maxScatterInstances = MicroScatterHighBudget;

        [SerializeField]
        [Tooltip("Stable seed used by the spatial hash when jittering cell placement.")]
        private uint scatterSeed = 149521u;

        [Header("Biome GPU Bindings")]
        [SerializeField]
        [Tooltip("Packed biome ground albedo/smoothness array. The terrain shader samples exactly one slice per pixel through IGN selection.")]
        private Texture2DArray biomeGroundTextureArray;

        [SerializeField, Range(0.005f, 1f)]
        [Tooltip("World-space tiling used when sampling the biome Texture2DArray.")]
        private float biomeGroundTextureScale = 0.1f;

        [SerializeField]
        [Tooltip("Fallback biome fog tint pushed to _CurrentBiomeColor when the monolith heatmap is not resident.")]
        private Color fallbackCurrentBiomeColor = new Color(0.15f, 0.19f, 0.21f, 1f);

        [Header("GPU Culling")]
        [SerializeField, Range(24f, 120f)]
        [Tooltip("Absolute distance limit for generated scatter instances.")]
        private float maxVisibleDistance = 58f;

        [SerializeField, Range(8f, 64f)]
        [Tooltip("Distance after which peripheral cone culling starts rejecting off-axis scatter.")]
        private float peripheralCullDistance = 30f;

        [SerializeField, Range(-1f, 1f)]
        [Tooltip("Minimum dot product against the camera forward vector required beyond the peripheral cull distance.")]
        private float peripheralCullDot = 0.5f;

        [Header("Foveated Occlusion")]
        [SerializeField]
        [Tooltip("If enabled, off-center scatter reuses cached visibility for three out of four frames.")]
        private bool enableFoveatedVisibilityCache = true;

        [SerializeField, Range(0.15f, 0.75f)]
        [Tooltip("Squared screen-space center radius is derived from this value. Outside it, visibility updates are throttled.")]
        private float foveatedInnerRadius = 0.4f;

        [SerializeField, Range(0f, 16f)]
        [Tooltip("Distance band in meters over which far scatter dissolves through deterministic dither instead of popping.")]
        private float farDitherBandMeters = 8f;

        [SerializeField]
        [Tooltip("If enabled, the scatter compute samples the previous camera depth pyramid and rejects hidden instances.")]
        private bool enableDepthOcclusion = true;

        [SerializeField, Range(0.02f, 2f)]
        [Tooltip("Eye-space depth bias in meters used to prevent false positive Hi-Z occlusion on near-contact scatter.")]
        private float occlusionDepthBias = 0.35f;

        [SerializeField, Range(0f, 6f)]
        [Tooltip("World-space frustum padding in meters to stabilize peripheral scatter during fast camera turns and TAA jitter.")]
        private float frustumPaddingMeters = 2f;

        [SerializeField, Range(0f, 8f)]
        [Tooltip("Projected radius threshold in pixels. Smaller scatter is culled before terrain-normal sampling.")]
        private float minProjectedPixelRadius = 2f;

        [SerializeField]
        [Tooltip("Pre-baked species bounds packed as center.xyz and radius. Runtime does not read Mesh.bounds for scatter culling.")]
        private Vector4 scatterSpeciesBounds = new Vector4(0f, 0.5f, 0f, 1f);

        [Header("Shadows")]
        [SerializeField]
        [Tooltip("Shadow mode used by the indirect draw.")]
        private ShadowCastingMode shadowCastingMode = ShadowCastingMode.On;

        [SerializeField]
        [Tooltip("Whether generated scatter receives shadows.")]
        private bool receiveShadows = true;

        [Header("Diagnostics")]
        [SerializeField] private int _debugGridResolution;
        [SerializeField] private int _debugVisibleCount;
        [SerializeField] private Bounds _debugDrawBounds;

        private bool _registered;
        private bool _registeredHotSwapListener;
        private int _clearDensityKernel = -1;
        private int _generateKernel = -1;
        private int _compactKernel = -1;
        private int _gridResolution;
        private GraphicsBuffer _instanceBuffer;
        private GraphicsBuffer _visibleIndicesBuffer;
        private GraphicsBuffer _visibilityCacheBuffer;
        private GraphicsBuffer _scatterDensityBuffer;
        private GraphicsBuffer _scatterBoundsLutBuffer;
        private GraphicsBuffer _argsBuffer;
        private GraphicsBuffer _modInstanceMatrixBufferA;
        private GraphicsBuffer _modInstanceMatrixBufferB;
        private float4x4[] _modInstanceMatrices;
        private readonly Plane[] _frustumPlaneCache = new Plane[FrustumPlaneCount]; // COLD ALLOC: Plane[6] - reusable frustum plane cache for GPU scatter dispatch - owner: GPUScatterDirector
        private readonly Vector4[] _frustumPlaneUpload = new Vector4[FrustumPlaneCount]; // COLD ALLOC: Vector4[6] - reusable GPU frustum plane upload payload for GPU scatter dispatch - owner: GPUScatterDirector
        private int _modInstanceCount;
        private int _lastUploadedModInstanceCount = -1;
        private int _modInstanceUploadBufferIndex;
        private int _lastRequestedGrid = -1;
        private int _lastClampedCapacity = -1;
        private int _lastResolvedCapacity = -1;
        private Mesh _argsBufferMesh;
        private RenderTexture _depthPyramidTexture;
        private int _depthPyramidWidth;
        private int _depthPyramidHeight;
        private int _depthPyramidMipCount;
        private int _depthPyramidCopyKernel = -1;
        private int _depthPyramidDownsampleKernel = -1;
        private int _depthPyramidInvalidatedFrame = -1;
        private int _scatterFrameIndex;
        private Vector3 _lastFoveatedCenter;
        private Vector3 _lastFoveatedCameraForward;
        private int _lastFoveatedGridResolution;
        private bool _hasFoveatedVisibilitySnapshot;
        private AsyncGPUReadbackRequest _visibleCountReadbackRequest;
        private bool _visibleCountReadbackPending;
        private bool _hasUploadedScatterBounds;
        private Vector4 _lastUploadedScatterBounds;
        private Texture2D _biomeHeatmapTexture;
        private byte[] _biomeHeatmapUpload;
        private int _biomeHeatmapBlobBytes = -1;
        private ulong _biomeHeatmapBlobChecksum;
        private bool _biomeHeatmapUploaded;
        private bool _originShiftListenerRegistered;
        private bool _hasCurrentBiomeColor;
        private Color _lastCurrentBiomeColor;
        private int _lastCurrentBiomePixel = -1;
        private uint _lastCurrentBiomeHash;
        private VaultGenerationHandle<ScatterTelemetryEntry> _scatterTelemetryRingHandle;
        private IDataVault _dataVault;
        private int _scatterTelemetryCursor;
        private bool _scatterTelemetryDumped;
        private double2 _scatterAupGenerationOffsetXZDouble;
        private Vector2 _scatterStableCellBaseXZ;
        private uint _lastOriginShiftSequence;
        private int _lastResolvedScatterBudget = -1;
        private IPlayerRuntimeContext _playerRuntimeContext;
        private ITickDispatcher _dispatcher;
        private float _cachedGlobalQualityWeight01 = 1f;

        /// <summary>
        /// Attempts to expose the GPU-authored 1D scatter density buffer for vegetation-drag consumers.
        /// </summary>
        /// <param name="densityBuffer">GPU buffer containing encoded density bins.</param>
        /// <param name="binCount">Number of valid bins in the buffer.</param>
        /// <param name="densityParams">Packed decode parameters: x invMaxDistanceSq, y encodeScale, z maxDistance, w invBinCount.</param>
        /// <returns>True when the active scatter director owns a valid density buffer.</returns>
        public static bool TryGetSargassumDragDensityBuffer(out GraphicsBuffer densityBuffer, out int binCount, out Vector4 densityParams)
        {
            GPUScatterDirector instance = _activeInstance;
            if (instance == null || instance._scatterDensityBuffer == null)
            {
                densityBuffer = null;
                binCount = 0;
                densityParams = Vector4.zero;
                return false;
            }

            densityBuffer = instance._scatterDensityBuffer;
            binCount = SargassumDensityBinCount;
            densityParams = instance.ResolveDensityParams();
            return true;
        }

        private void Awake()
        {
            _activeInstance = this;
#if UNITY_EDITOR
            TryAutoAssignAssets();
#endif
            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();
            ResolveDependencies();
            EnsureScatterTelemetryResources();
            EnsureResources();
            EnsureModInstanceResources();
            RefreshAupGridOffsetFromOrigin();
            TryRegisterOriginShiftListener();
            TryRegister();
        }

        private void OnEnable()
        {
            _activeInstance = this;
#if UNITY_EDITOR
            TryAutoAssignAssets();
#endif
            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();
            ResolveDependencies();
            EnsureScatterTelemetryResources();
            EnsureResources();
            EnsureModInstanceResources();
            RefreshAupGridOffsetFromOrigin();
            TryRegisterOriginShiftListener();
            TryRegister();
        }

        private void OnDisable()
        {
            if (_activeInstance == this)
                _activeInstance = null;

            TryUnregister();
            TryUnregisterHotSwapListener();
            TryUnregisterOriginShiftListener();
            ReleaseResources();
            _playerRuntimeContext = null;
            _dispatcher = null;
        }

        private void OnDestroy()
        {
            if (_activeInstance == this)
                _activeInstance = null;

            TryUnregister();
            TryUnregisterHotSwapListener();
            TryUnregisterOriginShiftListener();
            ReleaseResources();
            _playerRuntimeContext = null;
            _dispatcher = null;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Player:
                    _playerRuntimeContext = currentService as IPlayerRuntimeContext;
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    _dispatcher = currentService as ITickDispatcher;
                    _registered = false;
                    if (currentService != null && isActiveAndEnabled)
                        TryRegister();
                    break;
                case GlobalRegistryServiceSlot.DataVault:
                    ReleaseScatterTelemetryResources();
                    _dataVault = currentService as IDataVault;
                    EnsureScatterTelemetryResources();
                    break;
            }
        }

        /// <summary>
        /// Generates and renders the current player-centered scatter field.
        /// </summary>
        public void LateFrameTick()
        {
            float deltaTime = SystemDispatcher.CurrentFrameDeltaTime;
            if (deltaTime < 0f)
                return;

            if (HasMissingRuntimeDependencies())
                ResolveDependencies();

            EnsureResources();
            if (_modInstanceMatrices == null || _modInstanceMatrixBufferA == null || _modInstanceMatrixBufferB == null)
                EnsureModInstanceResources();

            FlushModInstanceLayer();
            if (scatterCompute == null ||
                _clearDensityKernel < 0 ||
                _generateKernel < 0 ||
                _compactKernel < 0 ||
                scatterMesh == null ||
                scatterMaterial == null ||
                _instanceBuffer == null ||
                _visibleIndicesBuffer == null ||
                _visibilityCacheBuffer == null ||
                _scatterDensityBuffer == null ||
                _scatterBoundsLutBuffer == null ||
                _argsBuffer == null ||
                viewCamera == null ||
                playerTransform == null ||
                vegetationBridge == null ||
                !vegetationBridge.TryGetActiveHeightTexturePayload(out HectonMapMagicVegetationBridge.TerrainHeightTexturePayload heightPayload))
            {
                _debugVisibleCount = 0;
                Vector3 fallbackCenter = playerTransform != null ? playerTransform.position : Vector3.zero;
                RecordScatterTelemetry(fallbackCenter, 0f, 0f, _gridResolution, 0, _lastCurrentBiomeHash, 0u, ScatterTelemetryMissingDependencyFlag);
                return;
            }

            PopulateFrustumPlaneUpload(viewCamera);

            Transform player = playerTransform;
            Transform cameraTransform = viewCamera.transform;
            Vector3 center = player.position;
            _cachedGlobalQualityWeight01 = ResolveGlobalQualityWeight01();
            float microScatterCullDistance = ResolveMicroScatterCullDistanceMeters();
            float activeScatterRadius = math.min(math.max(1f, scatterRadiusMeters), microScatterCullDistance);
            float activeCellSizeMeters = ResolveActiveCellSizeMeters(activeScatterRadius);
            float diameter = activeCellSizeMeters * math.max(1, _gridResolution);
            float halfDiameter = diameter * 0.5f;
            float minX = ResolveAupSnappedAxis(center.x - halfDiameter, _scatterAupGenerationOffsetXZDouble.x, activeCellSizeMeters);
            float minZ = ResolveAupSnappedAxis(center.z - halfDiameter, _scatterAupGenerationOffsetXZDouble.y, activeCellSizeMeters);
            _scatterStableCellBaseXZ = ResolveAupStableCellBaseXZ(minX, minZ, _scatterAupGenerationOffsetXZDouble, activeCellSizeMeters);
            Vector4 fieldRect = new Vector4(minX, minZ, diameter, diameter);
            int candidateCount = _gridResolution * _gridResolution;
            int heightResolution = math.max(1, heightPayload.HeightmapResolution);
            int heightMaxPixel = math.max(0, heightResolution - 1);
            float heightResolutionMinusOne = math.max(1f, heightMaxPixel);
            float heightTexelSize = math.rcp(heightResolutionMinusOne);
            Vector3 terrainSize = heightPayload.TerrainSize;
            float terrainSizeX = math.isfinite(terrainSize.x) ? math.max(terrainSize.x, 0.001f) : 0.001f;
            float terrainSizeZ = math.isfinite(terrainSize.z) ? math.max(terrainSize.z, 0.001f) : 0.001f;
            TryEnsureBiomeHeatmapTexture();
            PublishBiomeGlobals(in heightPayload, center);
            float configuredMinimumNormalY = math.isfinite(minimumNormalY) ? minimumNormalY : ScatterMinimumNormalY;
            float configuredMaxVisibleDistance = math.min(math.isfinite(maxVisibleDistance) ? maxVisibleDistance : 1f, microScatterCullDistance);
            float configuredPeripheralCullDistance = math.isfinite(peripheralCullDistance) ? peripheralCullDistance : 0f;
            float configuredPeripheralCullDot = math.isfinite(peripheralCullDot) ? peripheralCullDot : 0.5f;
            float safeMinimumNormalY = math.max(math.saturate(configuredMinimumNormalY), ScatterMinimumNormalY);
            float maxVisibleDistanceMeters = math.max(1f, configuredMaxVisibleDistance);
            float peripheralCullDistanceMeters = math.min(math.max(0f, configuredPeripheralCullDistance), maxVisibleDistanceMeters);
            float maxVisibleDistanceSq = maxVisibleDistanceMeters * maxVisibleDistanceMeters;
            float ditherBandMeters = math.clamp(
                math.isfinite(farDitherBandMeters) ? farDitherBandMeters : 0f,
                0f,
                maxVisibleDistanceMeters);
            float ditherStartMeters = math.max(0f, maxVisibleDistanceMeters - ditherBandMeters);
            float ditherStartSq = ditherStartMeters * ditherStartMeters;
            float ditherDenominatorSq = math.max(0.0001f, maxVisibleDistanceSq - ditherStartSq);
            float invDitherDenominatorSq = ditherBandMeters > 0.0001f ? math.rcp(ditherDenominatorSq) : 0f;
            int frameIndex = _scatterFrameIndex & 0x3fffffff;
            bool forceFullFoveatedUpdate = ResolveForceFullFoveatedUpdate(center, cameraTransform.forward);
            bool depthPyramidReady = BuildDepthPyramid(viewCamera);
            float screenWidth = math.max(1f, viewCamera.pixelWidth);
            float screenHeight = math.max(1f, viewCamera.pixelHeight);
            float projectionScalePixels = math.abs(viewCamera.projectionMatrix.m11) * screenHeight * 0.5f;
            float foveatedRadius = math.max(0f, math.isfinite(foveatedInnerRadius) ? foveatedInnerRadius : 0.4f);
            float foveatedRadiusSq = foveatedRadius * foveatedRadius;
            float foveatedGateSq = enableFoveatedVisibilityCache ? foveatedRadiusSq : 999f;
            int frameQuadrant = frameIndex & 3;
            Vector4 densityParams = ResolveDensityParams(maxVisibleDistanceSq, maxVisibleDistanceMeters);

            _visibleIndicesBuffer.SetCounterValue(0u);
            scatterCompute.SetBuffer(_clearDensityKernel, _ScatterDensityBinsId, _scatterDensityBuffer);
            scatterCompute.SetInt(_ScatterDensityBinCountId, SargassumDensityBinCount);
            scatterCompute.Dispatch(_clearDensityKernel, math.max(1, (SargassumDensityBinCount + ThreadGroupSize - 1) / ThreadGroupSize), 1, 1);

            scatterCompute.SetTexture(_generateKernel, _HeightTextureId, heightPayload.HeightTexture);
            scatterCompute.SetBuffer(_generateKernel, _ScatterInstancesId, _instanceBuffer);
            scatterCompute.SetBuffer(_generateKernel, _VisibleIndicesId, _visibleIndicesBuffer);
            scatterCompute.SetBuffer(_generateKernel, _VisibilityCacheId, _visibilityCacheBuffer);
            scatterCompute.SetBuffer(_generateKernel, _ScatterDensityBinsId, _scatterDensityBuffer);
            scatterCompute.SetBuffer(_generateKernel, _ScatterBoundsLutId, _scatterBoundsLutBuffer);
            scatterCompute.SetInt(_HeightMaxPixelId, heightMaxPixel);
            scatterCompute.SetFloat(_HeightResolutionMinusOneId, heightResolutionMinusOne);
            scatterCompute.SetFloat(_HeightTexelSizeId, heightTexelSize);
            scatterCompute.SetVector(_TerrainPositionId, heightPayload.TerrainPosition);
            scatterCompute.SetVector(_TerrainSizeId, terrainSize);
            scatterCompute.SetVector(_TerrainSizeInvXZId, new Vector4(math.rcp(terrainSizeX), math.rcp(terrainSizeZ), 0f, 0f));
            scatterCompute.SetVector(_FieldRectId, fieldRect);
            scatterCompute.SetInt(_GridResolutionId, _gridResolution);
            scatterCompute.SetInt(_CandidateCountId, candidateCount);
            scatterCompute.SetFloat(_CellSizeId, activeCellSizeMeters);
            scatterCompute.SetInt(_ScatterSeedId, unchecked((int)scatterSeed));
            scatterCompute.SetVector(_ScaleRangeId, new Vector4(math.min(minScale, maxScale), math.max(minScale, maxScale), 0f, 0f));
            scatterCompute.SetFloat(_MinNormalYSqId, safeMinimumNormalY * safeMinimumNormalY);
            scatterCompute.SetVector(_CameraPositionId, cameraTransform.position);
            scatterCompute.SetVector(_CameraForwardId, cameraTransform.forward);
            scatterCompute.SetFloat(_MaxDistanceSqId, maxVisibleDistanceSq);
            scatterCompute.SetFloat(_PeripheralDistanceSqId, peripheralCullDistanceMeters * peripheralCullDistanceMeters);
            scatterCompute.SetFloat(_PeripheralDotId, math.clamp(configuredPeripheralCullDot, -1f, 1f));
            scatterCompute.SetMatrix(_ViewProjectionId, GL.GetGPUProjectionMatrix(viewCamera.projectionMatrix, false) * viewCamera.worldToCameraMatrix);
            scatterCompute.SetMatrix(_ViewMatrixId, viewCamera.worldToCameraMatrix);
            scatterCompute.SetVector(_ScreenParamsId, new Vector4(screenWidth, screenHeight, ResolveMinProjectedPixelRadius(), projectionScalePixels));
            scatterCompute.SetVector(_FoveatedParamsId, new Vector4(foveatedGateSq, frameIndex, forceFullFoveatedUpdate ? 1f : 0f, 0f));
            scatterCompute.SetVector(_DitherParamsId, new Vector4(ditherStartSq, invDitherDenominatorSq, frameIndex, 0f));
            scatterCompute.SetVector(_BiomeHeatmapRectId, ResolveBiomeHeatmapRect(in heightPayload));
            scatterCompute.SetVector(_ScatterBiomeParamsId, ResolveScatterBiomeParams());
            scatterCompute.SetVector(_ScatterAupGridOffsetId, new Vector4(_scatterStableCellBaseXZ.x, _scatterStableCellBaseXZ.y, _lastOriginShiftSequence, 0f));
            if (_biomeHeatmapTexture != null)
                scatterCompute.SetTexture(_generateKernel, _BiomeHeatmapTexId, _biomeHeatmapTexture);
            scatterCompute.SetFloat(_ScatterFrustumPaddingId, math.max(0f, frustumPaddingMeters));
            scatterCompute.SetFloat(_ScatterOcclusionDepthBiasId, math.max(0.001f, occlusionDepthBias));
            scatterCompute.SetInt(_ScatterOcclusionEnabledId, depthPyramidReady && enableDepthOcclusion ? 1 : 0);
            scatterCompute.SetInt(_ScatterFrameQuadrantId, frameQuadrant);
            scatterCompute.SetInt(_ScatterDensityBinCountId, SargassumDensityBinCount);
            scatterCompute.SetVector(_ScatterDensityParamsId, densityParams);
            scatterCompute.SetInt(_ScatterBoundsLutCountId, ScatterBoundsLutCount);
            scatterCompute.SetVector(_ScatterZBufferParamsId, Shader.GetGlobalVector(_GlobalZBufferParamsId));
            if (_depthPyramidTexture != null)
                scatterCompute.SetTexture(_generateKernel, _ScatterDepthPyramidId, _depthPyramidTexture);
            scatterCompute.SetInt(_ScatterDepthPyramidMipCountId, depthPyramidReady ? _depthPyramidMipCount : 0);
            scatterCompute.SetVector(_ScatterDepthPyramidTexelSizeId, new Vector4(
                _depthPyramidWidth > 0 ? math.rcp(_depthPyramidWidth) : 0f,
                _depthPyramidHeight > 0 ? math.rcp(_depthPyramidHeight) : 0f,
                _depthPyramidWidth,
                _depthPyramidHeight));
            scatterCompute.SetVectorArray(_FrustumPlanesId, _frustumPlaneUpload);

            int dispatchGroups = math.max(1, (candidateCount + ThreadGroupSize - 1) / ThreadGroupSize);
            scatterCompute.Dispatch(_generateKernel, dispatchGroups, 1, 1);
            scatterCompute.SetBuffer(_compactKernel, _ScatterInstancesId, _instanceBuffer);
            scatterCompute.SetBuffer(_compactKernel, _VisibleIndicesId, _visibleIndicesBuffer);
            scatterCompute.SetBuffer(_compactKernel, _VisibilityCacheId, _visibilityCacheBuffer);
            scatterCompute.SetBuffer(_compactKernel, _ScatterDensityBinsId, _scatterDensityBuffer);
            scatterCompute.SetInt(_CandidateCountId, candidateCount);
            scatterCompute.SetInt(_ScatterDensityBinCountId, SargassumDensityBinCount);
            scatterCompute.SetVector(_CameraPositionId, cameraTransform.position);
            scatterCompute.SetVector(_ScatterDensityParamsId, densityParams);
            scatterCompute.Dispatch(_compactKernel, dispatchGroups, 1, 1);
            CommitFoveatedSnapshot(center, cameraTransform.forward);
            _scatterFrameIndex = (_scatterFrameIndex + 1) & 0x3fffffff;

            GraphicsBuffer.CopyCount(_visibleIndicesBuffer, _argsBuffer, sizeof(uint));
            UpdateVisibleCountReadback(frameIndex);
            Shader.SetGlobalBuffer(_ScatterInstancesId, _instanceBuffer);
            Shader.SetGlobalBuffer(_VisibleIndicesId, _visibleIndicesBuffer);
            Shader.SetGlobalBuffer(_ScatterDensityBinsId, _scatterDensityBuffer);
            Shader.SetGlobalVector(_ScatterDensityParamsId, densityParams);

            float terrainTop = heightPayload.TerrainPosition.y + heightPayload.TerrainSize.y;
            Bounds drawBounds = new Bounds(
                new Vector3(center.x, heightPayload.TerrainPosition.y + heightPayload.TerrainSize.y * 0.5f, center.z),
                new Vector3(diameter, math.max(8f, terrainTop - heightPayload.TerrainPosition.y), diameter));

            RenderParams renderParams = new RenderParams(scatterMaterial)
            {
                worldBounds = drawBounds,
                layer = gameObject.layer,
                shadowCastingMode = shadowCastingMode,
                receiveShadows = receiveShadows,
                camera = viewCamera
            };
            UnityEngine.Graphics.RenderMeshIndirect(renderParams, scatterMesh, _argsBuffer, 1, 0);

            _debugGridResolution = _gridResolution;
            _debugDrawBounds = drawBounds;
            RecordScatterTelemetry(center, activeScatterRadius, activeCellSizeMeters, _gridResolution, candidateCount, _lastCurrentBiomeHash, (uint)math.max(0, _debugVisibleCount), 0u);
        }

        /// <summary>
        /// Adds one mod-authored matrix to the reserved GPU instancing layer.
        /// </summary>
        public static bool SubmitModInstanceMatrix(uint modHash, uint resourceHash, in float4x4 matrix)
        {
            GPUScatterDirector instance = _activeInstance;
            if (instance == null || modHash == 0u || resourceHash == 0u)
                return false;

            return instance.TrySubmitModInstanceMatrix(in matrix);
        }

        private bool HasMissingRuntimeDependencies()
        {
            return vegetationBridge == null || playerTransform == null || viewCamera == null;
        }

        private void ResolveDependencies()
        {
            WorldRuntimeReferenceUtility.TryResolveHectonMapMagicVegetationBridge(ref vegetationBridge);
            WorldRuntimeReferenceUtility.TryResolvePlayerTransform(ref playerTransform);

            if (viewCamera == null && playerTransform != null)
            {
                IPlayerRuntimeContext playerContext = _playerRuntimeContext;
                viewCamera = playerContext != null && playerContext.PlayerCamera != null
                    ? playerContext.PlayerCamera
                    : ComponentReferenceUtility.ResolveOwnedComponent<Camera>(playerTransform);
            }
        }

        private void EnsureResources()
        {
            if (scatterCompute == null || scatterMesh == null || scatterMaterial == null)
                return;

            if (_generateKernel < 0)
                _generateKernel = scatterCompute.FindKernel("GenerateScatterInstances");

            if (_clearDensityKernel < 0)
                _clearDensityKernel = scatterCompute.FindKernel("ClearScatterDensityBuffer");

            if (_compactKernel < 0)
                _compactKernel = scatterCompute.FindKernel("CompactVisibleScatterInstances");

            ResolveDepthPyramidKernels();

            int requestedGrid = math.max(8, Mathf.CeilToInt((scatterRadiusMeters * 2f) / math.max(0.25f, cellSizeMeters)));
            int requestedCapacity = requestedGrid * requestedGrid;
            int resolvedScatterBudget = ResolveScatterInstanceBudget();
            int clampedCapacity = math.min(math.max(1, resolvedScatterBudget), requestedCapacity);
            bool capacityDirty =
                _lastRequestedGrid != requestedGrid ||
                _lastClampedCapacity != clampedCapacity ||
                _lastResolvedScatterBudget != resolvedScatterBudget ||
                _lastResolvedCapacity <= 0;

            int resolvedCapacity = _lastResolvedCapacity;
            if (capacityDirty)
            {
                _gridResolution = ResolveGridResolution(requestedGrid, clampedCapacity);
                resolvedCapacity = _gridResolution * _gridResolution;
                _lastRequestedGrid = requestedGrid;
                _lastClampedCapacity = clampedCapacity;
                _lastResolvedScatterBudget = resolvedScatterBudget;
                _lastResolvedCapacity = resolvedCapacity;
            }

            EnsureInstanceBufferCapacity(resolvedCapacity);
            EnsureVisibleIndexBufferCapacity(resolvedCapacity);
            EnsureVisibilityCacheBufferCapacity(resolvedCapacity);
            EnsureScatterDensityBuffer();
            EnsureScatterBoundsLutBuffer();
            EnsureIndirectArgsBuffer();
        }

        private void EnsureModInstanceResources()
        {
            if (_modInstanceMatrices == null)
                _modInstanceMatrices = new float4x4[MaxModInstancesPerFrame]; // COLD ALLOC: float4x4[1024] - mod instancing CPU upload staging - owner: GPUScatterDirector

            if (_modInstanceMatrixBufferA == null)
                _modInstanceMatrixBufferA = new GraphicsBuffer(GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags.LockBufferForWrite, MaxModInstancesPerFrame, UnsafeUtility.SizeOf<float4x4>()); // COLD ALLOC: GraphicsBuffer[1024] - reserved mod instancing matrix layer A - owner: GPUScatterDirector
            if (_modInstanceMatrixBufferB == null)
                _modInstanceMatrixBufferB = new GraphicsBuffer(GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags.LockBufferForWrite, MaxModInstancesPerFrame, UnsafeUtility.SizeOf<float4x4>()); // COLD ALLOC: GraphicsBuffer[1024] - reserved mod instancing matrix layer B - owner: GPUScatterDirector
        }

        private bool TrySubmitModInstanceMatrix(in float4x4 matrix)
        {
            EnsureModInstanceResources();
            if (_modInstanceMatrices == null || _modInstanceCount >= MaxModInstancesPerFrame)
                return false;

            _modInstanceMatrices[_modInstanceCount] = matrix;
            _modInstanceCount++;
            return true;
        }

        private void FlushModInstanceLayer()
        {
            GraphicsBuffer writeBuffer = _modInstanceUploadBufferIndex == 0 ? _modInstanceMatrixBufferA : _modInstanceMatrixBufferB;
            if (writeBuffer == null || _modInstanceMatrices == null)
                return;

            if (_modInstanceCount > 0)
            {
                GraphicsBufferUploadUtility.UploadArray(writeBuffer, _modInstanceMatrices, _modInstanceCount);
                Shader.SetGlobalBuffer(_ModInstanceMatricesId, writeBuffer);
                _modInstanceUploadBufferIndex ^= 1;
            }

            if (_lastUploadedModInstanceCount != _modInstanceCount)
            {
                Shader.SetGlobalInt(_ModInstanceCountId, _modInstanceCount);
                _lastUploadedModInstanceCount = _modInstanceCount;
            }

            _modInstanceCount = 0;
        }

        private void EnsureInstanceBufferCapacity(int requiredCapacity)
        {
            if (_instanceBuffer != null && _instanceBuffer.count >= requiredCapacity)
                return;

            ReleaseBuffer(ref _instanceBuffer);
            _instanceBuffer = GraphicsBufferUploadUtility.CreateStructuredBuffer<ScatterInstanceGpuData>(requiredCapacity); // COLD ALLOC: GraphicsBuffer[gridResolution^2] - persistent GPU scatter instance payload buffer - owner: GPUScatterDirector
        }

        private void EnsureVisibleIndexBufferCapacity(int requiredCapacity)
        {
            if (_visibleIndicesBuffer != null && _visibleIndicesBuffer.count >= requiredCapacity)
                return;

            ReleaseBuffer(ref _visibleIndicesBuffer);
            _visibleIndicesBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Append, requiredCapacity, UnsafeUtility.SizeOf<uint>()); // COLD ALLOC: GraphicsBuffer[gridResolution^2] - append visible-index buffer for GPU scatter indirect draw - owner: GPUScatterDirector
        }

        private void EnsureVisibilityCacheBufferCapacity(int requiredCapacity)
        {
            if (_visibilityCacheBuffer != null && _visibilityCacheBuffer.count >= requiredCapacity)
                return;

            ReleaseBuffer(ref _visibilityCacheBuffer);
            _visibilityCacheBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags.LockBufferForWrite, requiredCapacity, UnsafeUtility.SizeOf<uint>()); // COLD ALLOC: GraphicsBuffer[gridResolution^2] - persistent foveated scatter visibility cache - owner: GPUScatterDirector
            NativeArray<uint> cacheWrite = _visibilityCacheBuffer.LockBufferForWrite<uint>(0, requiredCapacity);
            try
            {
                for (int i = 0; i < requiredCapacity; i++)
                    cacheWrite[i] = 0u;
            }
            finally
            {
                _visibilityCacheBuffer.UnlockBufferAfterWrite<uint>(requiredCapacity);
            }
            _hasFoveatedVisibilitySnapshot = false;
        }

        private void EnsureScatterDensityBuffer()
        {
            if (_scatterDensityBuffer != null && _scatterDensityBuffer.count >= SargassumDensityBinCount)
                return;

            ReleaseBuffer(ref _scatterDensityBuffer);
            _scatterDensityBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, SargassumDensityBinCount, UnsafeUtility.SizeOf<uint>()); // COLD ALLOC: GraphicsBuffer[64] - GPU-authored 1D scatter density export for vegetation drag - owner: GPUScatterDirector
        }

        private void EnsureScatterBoundsLutBuffer()
        {
            if (_scatterBoundsLutBuffer == null)
            _scatterBoundsLutBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float4>(ScatterBoundsLutCount); // COLD ALLOC: GraphicsBuffer[16] - pre-baked scatter species bounds LUT - owner: GPUScatterDirector

            Vector4 safeBounds = ResolveSafeScatterBounds();
            if (_hasUploadedScatterBounds && _lastUploadedScatterBounds == safeBounds)
                return;

            NativeArray<float4> boundsWrite = _scatterBoundsLutBuffer.LockBufferForWrite<float4>(0, ScatterBoundsLutCount);
            try
            {
                float4 packedBounds = new float4(safeBounds.x, safeBounds.y, safeBounds.z, safeBounds.w);
                for (int i = 0; i < ScatterBoundsLutCount; i++)
                    boundsWrite[i] = packedBounds;
            }
            finally
            {
                _scatterBoundsLutBuffer.UnlockBufferAfterWrite<float4>(ScatterBoundsLutCount);
            }
            _lastUploadedScatterBounds = safeBounds;
            _hasUploadedScatterBounds = true;
        }

        private void EnsureIndirectArgsBuffer()
        {
            if (_argsBuffer == null)
                _argsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, GraphicsBuffer.UsageFlags.LockBufferForWrite, 1, GraphicsBuffer.IndirectDrawIndexedArgs.size); // COLD ALLOC: GraphicsBuffer[1] - indirect indexed draw args for GPU scatter - owner: GPUScatterDirector

            if (ReferenceEquals(_argsBufferMesh, scatterMesh))
                return;

            _argsBufferMesh = scatterMesh;
            NativeArray<GraphicsBuffer.IndirectDrawIndexedArgs> argsWrite =
                _argsBuffer.LockBufferForWrite<GraphicsBuffer.IndirectDrawIndexedArgs>(0, 1);
            try
            {
                argsWrite[0] = new GraphicsBuffer.IndirectDrawIndexedArgs
                {
                    indexCountPerInstance = scatterMesh != null ? scatterMesh.GetIndexCount(0) : 0u,
                    instanceCount = 0u,
                    startIndex = scatterMesh != null ? scatterMesh.GetIndexStart(0) : 0u,
                    baseVertexIndex = scatterMesh != null ? (uint)math.max(0, scatterMesh.GetBaseVertex(0)) : 0u,
                    startInstance = 0u
                };
            }
            finally
            {
                _argsBuffer.UnlockBufferAfterWrite<GraphicsBuffer.IndirectDrawIndexedArgs>(1);
            }
        }

        private void PopulateFrustumPlaneUpload(Camera cullCamera)
        {
            GeometryUtility.CalculateFrustumPlanes(cullCamera, _frustumPlaneCache);
            UploadFrustumPlane(0, 4);
            UploadFrustumPlane(1, 5);
            UploadFrustumPlane(2, 0);
            UploadFrustumPlane(3, 1);
            UploadFrustumPlane(4, 2);
            UploadFrustumPlane(5, 3);
        }

        private void UploadFrustumPlane(int targetIndex, int sourceIndex)
        {
            Plane plane = _frustumPlaneCache[sourceIndex];
            _frustumPlaneUpload[targetIndex] = new Vector4(plane.normal.x, plane.normal.y, plane.normal.z, plane.distance);
        }

        private float ResolveMinProjectedPixelRadius()
        {
            float configured = math.isfinite(minProjectedPixelRadius) ? minProjectedPixelRadius : 2f;
            float q = ResolveScatterQualityCurve01();
            float survivalRadius = math.max(2f, configured);
            float overkillRadius = math.max(0.5f, configured * 0.75f);
            return math.lerp(survivalRadius, overkillRadius, q);
        }

        private float ResolveMicroScatterCullDistanceMeters()
        {
            float q = ResolveScatterQualityCurve01();
            return math.lerp(MicroScatterLowCullMeters, MicroScatterHighCullMeters, q);
        }

        private int ResolveScatterInstanceBudget()
        {
            float q = ResolveScatterQualityCurve01();
            float lowToMx350 = math.lerp(
                MicroScatterLowBudget,
                MicroScatterMx350Budget,
                math.smoothstep(0f, 0.35f, q));
            float mx350ToMid = math.lerp(
                lowToMx350,
                MicroScatterMidBudget,
                math.smoothstep(0.28f, 0.68f, q));
            float resolvedBudget = math.lerp(
                mx350ToMid,
                MicroScatterHighBudget,
                math.smoothstep(0.62f, 1f, q));
            int continuousBudget = (int)math.round(resolvedBudget);
            return math.min(math.max(256, maxScatterInstances), continuousBudget);
        }

        private float ResolveScatterQualityCurve01()
        {
            float q = math.saturate(math.isfinite(_cachedGlobalQualityWeight01) ? _cachedGlobalQualityWeight01 : 1f);
            return q * q * (3f - 2f * q);
        }

        private static float ResolveGlobalQualityWeight01()
        {
            float signalWeight = SignalBusRegistry.GlobalQualityWeight01;
            if (math.isfinite(signalWeight) && signalWeight > 0f)
                return math.saturate(signalWeight);

            float brainWeight = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.isfinite(brainWeight) ? brainWeight : 1f);
        }

        private float ResolveActiveCellSizeMeters(float activeScatterRadius)
        {
            if (_gridResolution <= 0)
                return math.max(0.25f, cellSizeMeters);

            return math.max(0.05f, (activeScatterRadius * 2f) * math.rcp(_gridResolution));
        }

        private static float ResolveAupSnappedAxis(float value, double absoluteOffset, float cellSize)
        {
            double safeCellSize = math.max(0.0001f, cellSize);
            double snappedAbsolute = math.floor(((double)value + absoluteOffset) / safeCellSize) * safeCellSize;
            return (float)(snappedAbsolute - absoluteOffset);
        }

        private static Vector2 ResolveAupStableCellBaseXZ(float minX, float minZ, double2 absoluteOffset, float cellSize)
        {
            double safeCellSize = math.max(0.0001f, cellSize);
            double invCellSize = 1.0 / safeCellSize;
            return new Vector2(
                (float)math.floor(((double)minX + absoluteOffset.x) * invCellSize),
                (float)math.floor(((double)minZ + absoluteOffset.y) * invCellSize));
        }

        private void RefreshAupGridOffsetFromOrigin()
        {
            double3 currentOffset = HectonFloatingOrigin.CurrentTotalOffsetDouble;
            _scatterAupGenerationOffsetXZDouble = new double2(currentOffset.x, currentOffset.z);
            _scatterStableCellBaseXZ = Vector2.zero;
            _lastOriginShiftSequence = HectonFloatingOrigin.CurrentShiftSequence;
            Shader.SetGlobalVector(_ScatterAupGridOffsetId, new Vector4(_scatterStableCellBaseXZ.x, _scatterStableCellBaseXZ.y, _lastOriginShiftSequence, 0f));
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            if (!isActiveAndEnabled)
                return;

            _scatterAupGenerationOffsetXZDouble = new double2(shiftData.NewTotalOffsetDouble.x, shiftData.NewTotalOffsetDouble.z);
            _scatterStableCellBaseXZ = Vector2.zero;
            _lastOriginShiftSequence = shiftData.Sequence;
            _depthPyramidInvalidatedFrame = shiftData.Frame;
            _scatterFrameIndex = 0;
            if (_hasFoveatedVisibilitySnapshot)
            {
                _lastFoveatedCenter += -shiftData.ShiftOffset;
                _hasFoveatedVisibilitySnapshot = false;
            }

            Shader.SetGlobalVector(_ScatterAupGridOffsetId, new Vector4(_scatterStableCellBaseXZ.x, _scatterStableCellBaseXZ.y, _lastOriginShiftSequence, 0f));
            Vector3 telemetryCenter = playerTransform != null ? playerTransform.position : Vector3.zero;
            RecordScatterTelemetry(telemetryCenter, 0f, 0f, _gridResolution, 0, _lastCurrentBiomeHash, (uint)math.max(0, _debugVisibleCount), ScatterTelemetryOriginShiftFlag);
        }

        private void TryEnsureBiomeHeatmapTexture()
        {
            EnsureBiomeHeatmapResources();
            int residentBytes = H8StaticDataArena.IsLoaded ? H8StaticDataArena.ByteLength : 0;
            ulong residentChecksum = H8StaticDataArena.IsLoaded ? H8StaticDataArena.Header.Checksum64 : 0UL;
            if (_biomeHeatmapTexture == null ||
                (_biomeHeatmapBlobBytes == residentBytes && _biomeHeatmapBlobChecksum == residentChecksum))
            {
                return;
            }

            if (H8StaticDataArena.IsLoaded)
            {
                int biomeSliceCapacity = biomeGroundTextureArray != null ? math.clamp(biomeGroundTextureArray.depth, 1, 255) : 255;
                for (int y = 0; y < BiomeHeatmapResolution; y++)
                {
                    int rowOffset = y * BiomeHeatmapResolution;
                    for (int x = 0; x < BiomeHeatmapResolution; x++)
                    {
                        _biomeHeatmapUpload[rowOffset + x] = H8StaticDataArena.TryGetBiomeHeatmapCell(x, y, out uint biomeHash)
                            ? ResolveBiomeHeatmapByte(biomeHash, biomeSliceCapacity)
                            : (byte)0;
                    }
                }

                _biomeHeatmapUploaded = true;
            }
            else
            {
                for (int i = 0; i < BiomeHeatmapPixelCount; i++)
                    _biomeHeatmapUpload[i] = 0;

                _biomeHeatmapUploaded = false;
            }

            _biomeHeatmapTexture.SetPixelData(_biomeHeatmapUpload, 0);
            _biomeHeatmapTexture.Apply(false, false);
            _biomeHeatmapBlobBytes = residentBytes;
            _biomeHeatmapBlobChecksum = residentChecksum;
            _lastCurrentBiomePixel = -1;
            _lastCurrentBiomeHash = 0u;
        }

        private void EnsureBiomeHeatmapResources()
        {
            if (_biomeHeatmapUpload == null || _biomeHeatmapUpload.Length < BiomeHeatmapPixelCount)
            {
                _biomeHeatmapUpload = new byte[BiomeHeatmapPixelCount]; // COLD ALLOC: byte[256x256] - Data Monolith biome heatmap upload staging - owner: GPUScatterDirector
                _biomeHeatmapBlobBytes = -1;
                _biomeHeatmapBlobChecksum = 0UL;
            }

            if (_biomeHeatmapTexture != null)
                return;

            _biomeHeatmapTexture = new Texture2D(BiomeHeatmapResolution, BiomeHeatmapResolution, TextureFormat.R8, false, true)
            {
                name = "__HectonBiomeHeatmapR8",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            }; // COLD ALLOC: Texture2D[R8 256x256] - GPU biome heatmap LUT for terrain/scatter shaders - owner: GPUScatterDirector
            _biomeHeatmapBlobBytes = -1;
            _biomeHeatmapBlobChecksum = 0UL;
        }

        private void PublishBiomeGlobals(in HectonMapMagicVegetationBridge.TerrainHeightTexturePayload heightPayload, Vector3 center)
        {
            if (_biomeHeatmapTexture != null)
                Shader.SetGlobalTexture(_BiomeHeatmapTexId, _biomeHeatmapTexture);

            if (biomeGroundTextureArray != null)
                Shader.SetGlobalTexture(_BiomeGroundArrayId, biomeGroundTextureArray);

            Shader.SetGlobalVector(_BiomeHeatmapRectId, ResolveBiomeHeatmapRect(in heightPayload));
            Shader.SetGlobalVector(_BiomeTextureParamsId, ResolveBiomeTextureParams());
            Shader.SetGlobalVector(_ScatterBiomeParamsId, ResolveScatterBiomeParams());

            Color biomeColor = ResolveCurrentBiomeColor(in heightPayload, center);
            if (!_hasCurrentBiomeColor || _lastCurrentBiomeColor != biomeColor)
            {
                Shader.SetGlobalColor(_CurrentBiomeColorId, biomeColor);
                Shader.SetGlobalColor(_CurrentBiomeColorPlainId, biomeColor);
                _lastCurrentBiomeColor = biomeColor;
                _hasCurrentBiomeColor = true;
            }
        }

        private static unsafe byte ResolveBiomeHeatmapByte(uint biomeHash, int fallbackSliceCapacity = 255)
        {
            if (biomeHash == 0u)
                return 0;

            if (TryResolveBiomeRecord(biomeHash, out H8BiomeRecord record))
            {
                return (byte)math.clamp((int)record.RecordIndex + 1, 1, 255);
            }

            uint folded = biomeHash ^ (biomeHash >> 8) ^ (biomeHash >> 16) ^ (biomeHash >> 24);
            uint safeFallbackCapacity = (uint)math.clamp(fallbackSliceCapacity, 1, 255);
            return (byte)(1u + folded % safeFallbackCapacity);
        }

        private static unsafe bool TryResolveBiomeRecord(uint biomeHash, out H8BiomeRecord record)
        {
            record = default;
            if (biomeHash == 0u)
                return false;

            ReadOnlySpan<H8BiomeRecord> records = H8StaticDataArena.GetSectionSpan<H8BiomeRecord>(H8DataSectionId.Biomes);

            if (records.Length <= 0)
                return false;

            int low = 0;
            int high = records.Length - 1;
            while (low <= high)
            {
                int mid = (low + high) >> 1;
                H8BiomeRecord candidate = records[mid];
                if (candidate.BiomeHash == biomeHash)
                {
                    record = candidate;
                    return true;
                }

                if (candidate.BiomeHash < biomeHash)
                    low = mid + 1;
                else
                    high = mid - 1;
            }

            return false;
        }

        private static Vector4 ResolveBiomeHeatmapRect(in HectonMapMagicVegetationBridge.TerrainHeightTexturePayload heightPayload)
        {
            Vector3 terrainSize = heightPayload.TerrainSize;
            float invX = math.rcp(math.max(terrainSize.x, 0.001f));
            float invZ = math.rcp(math.max(terrainSize.z, 0.001f));
            return new Vector4(heightPayload.TerrainPosition.x, heightPayload.TerrainPosition.z, invX, invZ);
        }

        private Vector4 ResolveBiomeTextureParams()
        {
            int sliceCount = biomeGroundTextureArray != null ? math.max(1, biomeGroundTextureArray.depth) : 0;
            float arrayEnabled = _biomeHeatmapUploaded && sliceCount > 0 ? 1f : 0f;
            return new Vector4(math.max(0.0001f, biomeGroundTextureScale), sliceCount, BiomeHeatmapResolution - 1, arrayEnabled);
        }

        private Vector4 ResolveScatterBiomeParams()
        {
            int sliceCount = biomeGroundTextureArray != null ? math.max(1, biomeGroundTextureArray.depth) : 0;
            return new Vector4(BiomeHeatmapResolution - 1, _biomeHeatmapUploaded ? 1f : 0f, sliceCount, 0f);
        }

        private Color ResolveCurrentBiomeColor(in HectonMapMagicVegetationBridge.TerrainHeightTexturePayload heightPayload, Vector3 center)
        {
            if (!TrySampleCurrentBiomeHash(in heightPayload, center, out uint biomeHash))
                return fallbackCurrentBiomeColor;

            if (TryResolveBiomeColor(biomeHash, out Color biomeColor))
                return biomeColor;

            byte biomeId = ResolveBiomeHeatmapByte(biomeHash);
            if (biomeId == 0)
                return fallbackCurrentBiomeColor;

            float id01 = biomeId * (1f / 255f);
            return new Color(
                0.08f + math.frac(id01 * 5.17f + 0.11f) * 0.16f,
                0.13f + math.frac(id01 * 3.31f + 0.37f) * 0.22f,
                0.18f + math.frac(id01 * 7.93f + 0.53f) * 0.24f,
                1f);
        }

        private bool TrySampleCurrentBiomeHash(in HectonMapMagicVegetationBridge.TerrainHeightTexturePayload heightPayload, Vector3 center, out uint biomeHash)
        {
            biomeHash = 0u;
            if (!H8StaticDataArena.IsLoaded)
                return false;

            Vector4 rect = ResolveBiomeHeatmapRect(in heightPayload);
            float u = math.saturate((center.x - rect.x) * rect.z);
            float v = math.saturate((center.z - rect.y) * rect.w);
            int x = math.clamp((int)(u * (BiomeHeatmapResolution - 1) + 0.5f), 0, BiomeHeatmapResolution - 1);
            int y = math.clamp((int)(v * (BiomeHeatmapResolution - 1) + 0.5f), 0, BiomeHeatmapResolution - 1);
            int pixel = y * BiomeHeatmapResolution + x;
            if (pixel == _lastCurrentBiomePixel && _lastCurrentBiomeHash != 0u)
            {
                biomeHash = _lastCurrentBiomeHash;
                return true;
            }

            if (!H8StaticDataArena.TryGetBiomeHeatmapCell(x, y, out biomeHash))
                return false;

            _lastCurrentBiomePixel = pixel;
            _lastCurrentBiomeHash = biomeHash;
            return biomeHash != 0u;
        }

        private static unsafe bool TryResolveBiomeColor(uint biomeHash, out Color biomeColor)
        {
            biomeColor = default;
            if (!TryResolveBiomeRecord(biomeHash, out H8BiomeRecord record))
                return false;

            if (!math.isfinite(record.LightScatterR) ||
                !math.isfinite(record.LightScatterG) ||
                !math.isfinite(record.LightScatterB))
            {
                return false;
            }

            biomeColor = new Color(
                math.saturate(record.LightScatterR),
                math.saturate(record.LightScatterG),
                math.saturate(record.LightScatterB),
                math.saturate(record.FogDensity > 0f ? record.FogDensity : 1f));
            return biomeColor.a > 0f;
        }

        private bool ResolveForceFullFoveatedUpdate(Vector3 center, Vector3 cameraForward)
        {
            if (!enableFoveatedVisibilityCache ||
                !_hasFoveatedVisibilitySnapshot ||
                _lastFoveatedGridResolution != _gridResolution)
            {
                return true;
            }

            float maxCenterDelta = math.max(0.01f, cellSizeMeters * 0.25f);
            float centerDeltaSq = math.lengthsq((float3)(center - _lastFoveatedCenter));
            if (centerDeltaSq > maxCenterDelta * maxCenterDelta)
                return true;

            return Vector3.Dot(_lastFoveatedCameraForward, cameraForward) < 0.996f;
        }

        private Vector4 ResolveDensityParams()
        {
            float configuredMaxVisibleDistance = math.isfinite(maxVisibleDistance) ? maxVisibleDistance : 1f;
            float maxVisibleDistanceMeters = math.max(1f, configuredMaxVisibleDistance);
            return ResolveDensityParams(maxVisibleDistanceMeters * maxVisibleDistanceMeters, maxVisibleDistanceMeters);
        }

        private static Vector4 ResolveDensityParams(float maxVisibleDistanceSq, float maxVisibleDistanceMeters)
        {
            float safeMaxDistanceSq = math.max(1f, maxVisibleDistanceSq);
            return new Vector4(
                math.rcp(safeMaxDistanceSq),
                SargassumDensityEncodeScale,
                math.max(1f, maxVisibleDistanceMeters),
                math.rcp(SargassumDensityBinCount));
        }

        private Vector4 ResolveSafeScatterBounds()
        {
            Vector4 bounds = scatterSpeciesBounds;
            if (!math.isfinite(bounds.x))
                bounds.x = 0f;
            if (!math.isfinite(bounds.y))
                bounds.y = 0.5f;
            if (!math.isfinite(bounds.z))
                bounds.z = 0f;
            if (!math.isfinite(bounds.w))
                bounds.w = 1f;

            bounds.w = math.max(0.05f, bounds.w);
            return bounds;
        }

        private void CommitFoveatedSnapshot(Vector3 center, Vector3 cameraForward)
        {
            _lastFoveatedCenter = center;
            _lastFoveatedCameraForward = cameraForward;
            _lastFoveatedGridResolution = _gridResolution;
            _hasFoveatedVisibilitySnapshot = true;
        }

        private void EnsureScatterTelemetryResources()
        {
            IDataVault vault = CacheDataVaultCold();
            if (vault == null)
                return;

            if (IsExactVaultHandle(in _scatterTelemetryRingHandle, ScatterTelemetryRingBufferId))
                return;

            ReleaseVaultHandle(vault, ref _scatterTelemetryRingHandle);
            _scatterTelemetryRingHandle = vault.EnsureGenerationHandle<ScatterTelemetryEntry>(
                ScatterTelemetryRingBufferId,
                ScatterTelemetryCapacity,
                VaultOwnerSystemId,
                NativeArrayOptions.ClearMemory); // COLD ALLOC: DataVault ScatterTelemetryEntry[300] - black-box ring for GPU terrain scatter state - owner: GPUScatterDirector
            _scatterTelemetryCursor = 0;
            _scatterTelemetryDumped = false;
        }

        private void RecordScatterTelemetry(
            Vector3 center,
            float radiusMeters,
            float cellSizeMeters,
            int gridResolution,
            int candidateCount,
            uint biomeHash,
            uint visibleCount,
            uint flags)
        {
            EnsureScatterTelemetryResources();
            if (!TryAcquireScatterTelemetryRingWrite(out NativeArray<ScatterTelemetryEntry> telemetryRing))
                return;

            float3 center3 = (float3)center;
            bool invalidState =
                !math.all(math.isfinite(center3)) ||
                !math.isfinite(radiusMeters) ||
                !math.isfinite(cellSizeMeters) ||
                radiusMeters < 0f ||
                cellSizeMeters < 0f ||
                gridResolution < 0 ||
                candidateCount < 0;
            uint resolvedFlags = invalidState ? flags | ScatterTelemetryInvalidStateFlag : flags;
            uint checksumLo = (uint)_biomeHeatmapBlobChecksum;
            uint stateHash = ScatterTelemetryHashSeed;
            stateHash = MixTelemetryHash(stateHash, (uint)math.max(0, gridResolution));
            stateHash = MixTelemetryHash(stateHash, (uint)math.max(0, candidateCount));
            stateHash = MixTelemetryHash(stateHash, biomeHash);
            stateHash = MixTelemetryHash(stateHash, visibleCount);
            stateHash = MixTelemetryHash(stateHash, unchecked((uint)_lastOriginShiftSequence));
            stateHash = MixTelemetryHash(stateHash, checksumLo);

            bool shouldDump = false;
            try
            {
                int safeCursor = math.clamp(_scatterTelemetryCursor, 0, telemetryRing.Length - 1);
                telemetryRing[safeCursor] = new ScatterTelemetryEntry
                {
                    Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                    Flags = resolvedFlags,
                    Center = center3,
                    AupOffsetXZ = (float2)_scatterStableCellBaseXZ,
                    RadiusMeters = radiusMeters,
                    CellSizeMeters = cellSizeMeters,
                    GridResolution = gridResolution,
                    CandidateCount = candidateCount,
                    BiomeHash = biomeHash,
                    VisibleCount = visibleCount,
                    StateHash = stateHash,
                    OriginShiftSequence = _lastOriginShiftSequence,
                    BlobChecksumLo = checksumLo
                };
                _scatterTelemetryCursor = safeCursor + 1;
                if (_scatterTelemetryCursor >= ScatterTelemetryCapacity || _scatterTelemetryCursor >= telemetryRing.Length)
                    _scatterTelemetryCursor = 0;

                shouldDump = (resolvedFlags & ScatterTelemetryInvalidStateFlag) != 0u;
            }
            finally
            {
                ReleaseScatterTelemetryRingWrite();
            }

            if (shouldDump)
                DumpScatterBlackBox();
        }

        private static uint MixTelemetryHash(uint hash, uint value)
        {
            hash ^= value;
            hash *= 16777619u;
            return hash;
        }

        private void DumpScatterBlackBox()
        {
            if (_scatterTelemetryDumped || !TryReadScatterTelemetryRing(out NativeArray<ScatterTelemetryEntry>.ReadOnly telemetryRing))
                return;

            _scatterTelemetryDumped = true;
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ScatterBlackBoxDumpRelativePath));
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(ScatterTelemetryCapacity);
                writer.Write(_scatterTelemetryCursor);
                for (int i = 0; i < ScatterTelemetryCapacity; i++)
                {
                    ScatterTelemetryEntry entry = telemetryRing[i];
                    writer.Write(entry.Frame);
                    writer.Write(entry.Flags);
                    writer.Write(entry.Center.x);
                    writer.Write(entry.Center.y);
                    writer.Write(entry.Center.z);
                    writer.Write(entry.AupOffsetXZ.x);
                    writer.Write(entry.AupOffsetXZ.y);
                    writer.Write(entry.RadiusMeters);
                    writer.Write(entry.CellSizeMeters);
                    writer.Write(entry.GridResolution);
                    writer.Write(entry.CandidateCount);
                    writer.Write(entry.BiomeHash);
                    writer.Write(entry.VisibleCount);
                    writer.Write(entry.StateHash);
                    writer.Write(entry.OriginShiftSequence);
                    writer.Write(entry.BlobChecksumLo);
                }
            }
        }

        private void ReleaseScatterTelemetryResources()
        {
            ReleaseVaultHandle(_dataVault, ref _scatterTelemetryRingHandle);
            _scatterTelemetryCursor = 0;
            _scatterTelemetryDumped = false;
        }

        private void ResolveDepthPyramidKernels()
        {
            if (depthPyramidCompute == null)
            {
                _depthPyramidCopyKernel = -1;
                _depthPyramidDownsampleKernel = -1;
                return;
            }

            if (_depthPyramidCopyKernel < 0)
                _depthPyramidCopyKernel = depthPyramidCompute.FindKernel("CopyDepthPyramidMip0");

            if (_depthPyramidDownsampleKernel < 0)
                _depthPyramidDownsampleKernel = depthPyramidCompute.FindKernel("DownsampleDepthPyramidMip");
        }

        private bool BuildDepthPyramid(Camera cullCamera)
        {
            if (!enableDepthOcclusion || depthPyramidCompute == null || cullCamera == null)
                return false;

            if (SystemDispatcher.CurrentFrameIndex <= _depthPyramidInvalidatedFrame)
                return false;

            ResolveDepthPyramidKernels();
            Texture depthTexture = Shader.GetGlobalTexture(_GlobalCameraDepthTextureId);
            if (depthTexture == null)
                return false;

            int targetWidth = math.max(1, cullCamera.pixelWidth);
            int targetHeight = math.max(1, cullCamera.pixelHeight);
            EnsureDepthPyramidResources(targetWidth, targetHeight);
            if (_depthPyramidTexture == null || _depthPyramidCopyKernel < 0 || _depthPyramidDownsampleKernel < 0)
                return false;

            depthPyramidCompute.SetTexture(_depthPyramidCopyKernel, _DepthPyramidSourceDepthId, depthTexture);
            depthPyramidCompute.SetTexture(_depthPyramidCopyKernel, _DepthPyramidTargetId, _depthPyramidTexture, 0);
            depthPyramidCompute.Dispatch(
                _depthPyramidCopyKernel,
                math.max(1, (_depthPyramidWidth + 7) / 8),
                math.max(1, (_depthPyramidHeight + 7) / 8),
                1);

            for (int mipIndex = 1; mipIndex < _depthPyramidMipCount; mipIndex++)
            {
                int mipWidth = math.max(1, _depthPyramidWidth >> mipIndex);
                int mipHeight = math.max(1, _depthPyramidHeight >> mipIndex);
                depthPyramidCompute.SetTexture(_depthPyramidDownsampleKernel, _DepthPyramidSourceId, _depthPyramidTexture, mipIndex - 1);
                depthPyramidCompute.SetTexture(_depthPyramidDownsampleKernel, _DepthPyramidTargetId, _depthPyramidTexture, mipIndex);
                depthPyramidCompute.Dispatch(
                    _depthPyramidDownsampleKernel,
                    math.max(1, (mipWidth + 7) / 8),
                    math.max(1, (mipHeight + 7) / 8),
                    1);
            }

            return true;
        }

        private void EnsureDepthPyramidResources(int targetWidth, int targetHeight)
        {
            if (targetWidth <= 0 || targetHeight <= 0)
                return;

            if (_depthPyramidTexture != null && _depthPyramidWidth == targetWidth && _depthPyramidHeight == targetHeight)
                return;

            ReleaseDepthPyramidTexture();
            _depthPyramidWidth = targetWidth;
            _depthPyramidHeight = targetHeight;
            _depthPyramidMipCount = ResolveMipCountNoLog(targetWidth, targetHeight);
            _depthPyramidTexture = new RenderTexture(targetWidth, targetHeight, 0, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear)
            {
                name = "__HectonScatterDepthPyramid",
                hideFlags = HideFlags.HideAndDontSave,
                enableRandomWrite = true,
                useMipMap = true,
                autoGenerateMips = false,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            }; // COLD ALLOC: RenderTexture[targetWidth x targetHeight] - scatter Hi-Z depth pyramid for compute occlusion - owner: GPUScatterDirector
            _depthPyramidTexture.Create();
        }

        private static int ResolveMipCountNoLog(int width, int height)
        {
            int size = math.max(1, math.max(width, height));
            int count = 1;
            count += size >= 2 ? 1 : 0;
            count += size >= 4 ? 1 : 0;
            count += size >= 8 ? 1 : 0;
            count += size >= 16 ? 1 : 0;
            count += size >= 32 ? 1 : 0;
            count += size >= 64 ? 1 : 0;
            count += size >= 128 ? 1 : 0;
            count += size >= 256 ? 1 : 0;
            count += size >= 512 ? 1 : 0;
            count += size >= 1024 ? 1 : 0;
            count += size >= 2048 ? 1 : 0;
            count += size >= 4096 ? 1 : 0;
            count += size >= 8192 ? 1 : 0;
            count += size >= 16384 ? 1 : 0;
            count += size >= 32768 ? 1 : 0;
            return count;
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying || _dispatcher == null)
                return;

            _registered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void CacheRegistryServicesCold()
        {
            if (_playerRuntimeContext == null)
                _playerRuntimeContext = GlobalRegistry.Player;

            if (_dispatcher == null)
                _dispatcher = GlobalRegistry.Dispatcher;

            if (_dataVault == null)
                _dataVault = GlobalRegistry.DataVault;

            _cachedGlobalQualityWeight01 = ResolveGlobalQualityWeight01();
        }

        private IDataVault CacheDataVaultCold()
        {
            if (_dataVault == null)
                _dataVault = GlobalRegistry.DataVault;
            return _dataVault;
        }

        private bool TryAcquireScatterTelemetryRingWrite(out NativeArray<ScatterTelemetryEntry> telemetryRing)
        {
            telemetryRing = default;
            IDataVault vault = _dataVault;
            if (vault == null ||
                !IsExactVaultHandle(in _scatterTelemetryRingHandle, ScatterTelemetryRingBufferId) ||
                !vault.TryAcquireWriteLock(in _scatterTelemetryRingHandle, VaultOwnerSystemId, out telemetryRing))
            {
                return false;
            }

            if (telemetryRing.Length >= ScatterTelemetryCapacity)
                return true;

            vault.ReleaseWriteLock(in _scatterTelemetryRingHandle, VaultOwnerSystemId);
            telemetryRing = default;
            return false;
        }

        private void ReleaseScatterTelemetryRingWrite()
        {
            IDataVault vault = _dataVault;
            if (vault != null && IsExactVaultHandle(in _scatterTelemetryRingHandle, ScatterTelemetryRingBufferId))
                vault.ReleaseWriteLock(in _scatterTelemetryRingHandle, VaultOwnerSystemId);
        }

        private bool TryReadScatterTelemetryRing(out NativeArray<ScatterTelemetryEntry>.ReadOnly telemetryRing)
        {
            telemetryRing = default;
            IDataVault vault = _dataVault;
            return vault != null &&
                   IsExactVaultHandle(in _scatterTelemetryRingHandle, ScatterTelemetryRingBufferId) &&
                   vault.TryReadOnlyHandle(in _scatterTelemetryRingHandle, out telemetryRing) &&
                   telemetryRing.Length >= ScatterTelemetryCapacity;
        }

        private static bool IsExactVaultHandle<T>(in VaultGenerationHandle<T> handle, BufferID expectedBufferId) where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)expectedBufferId) && handle.Generation != 0u;
        }

        private static void ReleaseVaultHandle<T>(IDataVault vault, ref VaultGenerationHandle<T> handle) where T : struct
        {
            if (vault != null && handle.BufferID != 0u && handle.Generation != 0u)
                vault.ReleaseBuffer(in handle);
            handle = default;
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

        private void TryRegisterOriginShiftListener()
        {
            if (_originShiftListenerRegistered || !Application.isPlaying)
                return;

            HectonFloatingOrigin.RegisterListener(this);
            _originShiftListenerRegistered = true;
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
            _registered = false;
        }

        private void TryUnregisterOriginShiftListener()
        {
            if (!_originShiftListenerRegistered)
                return;

            HectonFloatingOrigin.UnregisterListener(this);
            _originShiftListenerRegistered = false;
        }

        private void ReleaseResources()
        {
            ReleaseBuffer(ref _instanceBuffer);
            ReleaseBuffer(ref _visibleIndicesBuffer);
            ReleaseBuffer(ref _visibilityCacheBuffer);
            ReleaseBuffer(ref _scatterDensityBuffer);
            ReleaseBuffer(ref _scatterBoundsLutBuffer);
            ReleaseBuffer(ref _argsBuffer);
            ReleaseBuffer(ref _modInstanceMatrixBufferA);
            ReleaseBuffer(ref _modInstanceMatrixBufferB);
            ReleaseDepthPyramidTexture();
            ReleaseBiomeHeatmapResources();
            ReleaseScatterTelemetryResources();
            _modInstanceMatrices = null;

            _modInstanceCount = 0;
            _lastUploadedModInstanceCount = -1;
            _modInstanceUploadBufferIndex = 0;
            _lastRequestedGrid = -1;
            _lastClampedCapacity = -1;
            _lastResolvedCapacity = -1;
            _argsBufferMesh = null;
            _depthPyramidWidth = 0;
            _depthPyramidHeight = 0;
            _depthPyramidMipCount = 0;
            _depthPyramidInvalidatedFrame = -1;
            _scatterFrameIndex = 0;
            _hasFoveatedVisibilitySnapshot = false;
            _visibleCountReadbackPending = false;
            _hasUploadedScatterBounds = false;
            _lastUploadedScatterBounds = Vector4.zero;
            _hasCurrentBiomeColor = false;
            _lastResolvedScatterBudget = -1;
        }

        private static void ReleaseBuffer(ref GraphicsBuffer buffer)
        {
            if (buffer == null)
                return;

            buffer.Release();
            buffer = null;
        }

        private void ReleaseDepthPyramidTexture()
        {
            if (_depthPyramidTexture == null)
                return;

            _depthPyramidTexture.Release();
            if (Application.isPlaying)
                Destroy(_depthPyramidTexture);
            else
                DestroyImmediate(_depthPyramidTexture);

            _depthPyramidTexture = null;
        }

        private void ReleaseBiomeHeatmapResources()
        {
            if (_biomeHeatmapTexture != null)
            {
                if (Application.isPlaying)
                    Destroy(_biomeHeatmapTexture);
                else
                    DestroyImmediate(_biomeHeatmapTexture);

                _biomeHeatmapTexture = null;
            }

            _biomeHeatmapUpload = null;

            _biomeHeatmapBlobBytes = -1;
            _biomeHeatmapBlobChecksum = 0UL;
            _biomeHeatmapUploaded = false;
            _lastCurrentBiomePixel = -1;
            _lastCurrentBiomeHash = 0u;
        }

        private static int ResolveGridResolution(int requestedGrid, int clampedCapacity)
        {
            int high = math.max(1, requestedGrid);
            int low = 1;
            int best = 1;
            int safeCapacity = math.max(1, clampedCapacity);
            while (low <= high)
            {
                int mid = (low + high) >> 1;
                long candidateCount = (long)mid * mid;
                if (candidateCount <= safeCapacity)
                {
                    best = mid;
                    low = mid + 1;
                }
                else
                {
                    high = mid - 1;
                }
            }

            return best;
        }

        private void UpdateVisibleCountReadback(int frameIndex)
        {
            if (_visibleCountReadbackPending)
            {
                if (!_visibleCountReadbackRequest.done)
                    return;

                _visibleCountReadbackPending = false;
                if (!_visibleCountReadbackRequest.hasError)
                {
                    NativeArray<uint> argsData = _visibleCountReadbackRequest.GetData<uint>();
                    _debugVisibleCount = argsData.Length > IndirectArgsInstanceCountIndex
                        ? (int)math.min(argsData[IndirectArgsInstanceCountIndex], (uint)int.MaxValue)
                        : 0;
                }

                return;
            }

            if (_argsBuffer == null || (frameIndex % VisibleCountReadbackFrameStride) != 0)
                return;

            _visibleCountReadbackRequest = AsyncGPUReadback.Request(_argsBuffer);
            _visibleCountReadbackPending = true;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            minimumNormalY = math.max(minimumNormalY, ScatterMinimumNormalY);
            cellSizeMeters = math.max(0.25f, cellSizeMeters);
            maxScatterInstances = math.max(256, maxScatterInstances);
            biomeGroundTextureScale = math.max(0.0001f, biomeGroundTextureScale);
            maxVisibleDistance = math.max(1f, maxVisibleDistance);
            peripheralCullDistance = math.max(0f, peripheralCullDistance);
            farDitherBandMeters = math.clamp(farDitherBandMeters, 0f, maxVisibleDistance);
            minProjectedPixelRadius = math.max(0f, minProjectedPixelRadius);
            scatterSpeciesBounds = ResolveSafeScatterBounds();
            frustumPaddingMeters = math.max(0f, frustumPaddingMeters);
            occlusionDepthBias = math.max(0.001f, occlusionDepthBias);
            TryAutoAssignAssets();
        }

        private void TryAutoAssignAssets()
        {
            if (scatterCompute == null)
                scatterCompute = AssetDatabase.LoadAssetAtPath<ComputeShader>(ScatterComputeAssetPath);

            if (depthPyramidCompute == null)
                depthPyramidCompute = AssetDatabase.LoadAssetAtPath<ComputeShader>(DepthPyramidComputeAssetPath);

            _generateKernel = scatterCompute != null ? scatterCompute.FindKernel("GenerateScatterInstances") : -1;
            _clearDensityKernel = scatterCompute != null ? scatterCompute.FindKernel("ClearScatterDensityBuffer") : -1;
            _compactKernel = scatterCompute != null ? scatterCompute.FindKernel("CompactVisibleScatterInstances") : -1;
            _depthPyramidCopyKernel = depthPyramidCompute != null ? depthPyramidCompute.FindKernel("CopyDepthPyramidMip0") : -1;
            _depthPyramidDownsampleKernel = depthPyramidCompute != null ? depthPyramidCompute.FindKernel("DownsampleDepthPyramidMip") : -1;
        }
#endif
    }
}
