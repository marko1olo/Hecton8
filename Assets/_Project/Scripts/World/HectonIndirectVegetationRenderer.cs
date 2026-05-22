using System;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Gameplay;
using Hecton8.Optimization;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;
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
    public class HectonIndirectVegetationRenderer : MonoBehaviour, ITickable, IOriginShiftListener
    {
        /// <summary>Stride of one Matrix4x4 entry expected in the external instance matrix buffer.</summary>
        public const int InstanceMatrixStride = 64;

        /// <summary>Stride of one <see cref="HectonVegetationInstanceData"/> entry expected in the instance metadata buffer.</summary>
        public const int InstanceDataStride = HectonVegetationInstanceData.Stride;

        private const int IndirectArgsCount = 5;
        private const int VisibleIndexStride = sizeof(uint);
        private const int ThreadsPerGroup = 64;
        private const int FrustumPlaneCount = 6;
        private const int CpuCullingScratchPlaneCapacity = 16;
        private const int CpuCullingScratchBufferCount = 2;
        private const int BrgMetadataPlaceholderCount = 1;
        private const int MaxVegetationVisibilityPasses = 3;
        private const int MaxVegetationDrawCommands = 7;
        private const float LodTransitionRangeMeters = 2f;
        private const byte VisibilityMaskNear = 1 << 0;
        private const byte VisibilityMaskFar = 1 << 1;
        private const byte VisibilityMaskShadow = 1 << 2;
        private const int FloraGrowthTelemetryFrameCount = 300;
        private const int FloraGrowthTelemetryMaxSamples = 64;
        private const int FloraGrowthTelemetryDumpVersion = 2;
        private const uint FloraGrowthTelemetryHashSeed = 2166136261u;
        private const string FloraGrowthDumpRelativePath = "Docs/AgentLogs/Dump_FLORA_GROWTH_SYSTEM.bin";
        private const int ScatterCullTelemetryFrameCount = 300;
        private const int ScatterCullTelemetryCounterCount = 4;
        private const int ScatterCullTelemetryReadbackStrideFrames = 30;
        private const int ScatterCullTelemetryTotalCounter = 0;
        private const int ScatterCullTelemetryFrustumCounter = 1;
        private const int ScatterCullTelemetryOcclusionCounter = 2;
        private const int ScatterCullTelemetryVisibleCounter = 3;
        private const int ScatterCullOverdrawWarningVisibleCount = 50000;
        private const int MockScatterDefaultAxisCount = 100;
        private const float MockScatterDefaultSpacing = 2f;
        private const uint MockScatterDefaultSeed = 0x53484939u;
        private const string ScatterCullDumpRelativePath = "Docs/AgentLogs/Dump_SHINOBU_09.bin";
        private const string ScatterCullH8DumpRelativePath = "Docs/AgentLogs/Dump_SHINOBU_09.h8dump";
        private const Allocator DataVaultExemptVegetationMockScatterAllocator = Allocator.Persistent;
        private const Allocator DataVaultExemptVegetationBrgMetadataAllocator = Allocator.Persistent;
        private const Allocator DataVaultExemptVegetationAgeLaneAllocator = Allocator.Persistent;
        private const Allocator DataVaultExemptVegetationTelemetryAllocator = Allocator.Persistent;
        private const Allocator DataVaultExemptVegetationCpuCullingAllocator = Allocator.Persistent;
        private const Allocator DataVaultExemptVegetationCpuScratchAllocator = Allocator.Persistent;
#if UNITY_EDITOR
        private const string ComputeShaderAssetPath = "Assets/_Project/Art/Shaders/FloraCulling.compute";
        private const string AbyssalFlowFieldComputeAssetPath = "Assets/_Project/Art/Shaders/AbyssalFlowField.compute";
        private const string DepthPyramidComputeAssetPath = "Assets/_Project/Art/Shaders/Hecton_DepthPyramid.compute";
        private const string VegetationShaderAssetPath = "Assets/_Project/Art/Shaders/Hecton_IndirectVegetation.shader";
        private const string DepthShaderAssetPath = "Assets/_Project/Art/Shaders/Hecton_IndirectVegetationDepthOnly.shader";
        private const string ShadowShaderAssetPath = "Assets/_Project/Art/Shaders/Hecton_IndirectVegetationShadow.shader";
        private const string MotionShaderAssetPath = "Assets/_Project/Art/Shaders/Hecton_IndirectVegetationMotionVectors.shader";
        private const string VegetationMaterialAssetPath = "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_HectonIndirectVegetation.mat";
        private const string DepthMaterialAssetPath = "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_HectonIndirectVegetation_DepthOnly.mat";
        private const string ShadowMaterialAssetPath = "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_HectonIndirectVegetation_Shadow.mat";
        private const string MotionMaterialAssetPath = "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_HectonIndirectVegetation_MotionVectors.mat";
#endif
        private const string VegetationShaderName = "Hecton8/Vegetation/IndirectStrip";

        private static readonly int _InstanceMatricesId = Shader.PropertyToID("_HectonInstanceMatrices");
        private static readonly int _InstanceDataId = Shader.PropertyToID("_HectonVegetationInstanceData");
        private static readonly int _FloraPhaseSeedsId = Shader.PropertyToID("_HectonFloraPhaseSeeds");
        private static readonly int _FloraAges01Id = Shader.PropertyToID("_HectonFloraAges01");
        private static readonly int _FloraSnapFlagsId = Shader.PropertyToID("_HectonFloraSnapFlags");
        private static readonly int _FloraSnapFlagsEnabledId = Shader.PropertyToID("_HectonFloraSnapFlagsEnabled");
        private static readonly int _VisibleInstanceIndicesId = Shader.PropertyToID("_HectonVisibleInstanceIndices");
        private static readonly int _ChunkWorldOffsetId = Shader.PropertyToID("_ChunkWorldOffset");
        private static readonly int _GlobalFloatingOffsetId = Shader.PropertyToID("_GlobalFloatingOffset");
        private static readonly int _LodPassModeId = Shader.PropertyToID("_HectonLodPassMode");
        private static readonly int _LodNearDistanceId = Shader.PropertyToID("_HectonLodNearDistance");
        private static readonly int _LodFarDistanceId = Shader.PropertyToID("_HectonLodFarDistance");
        private static readonly int _LodTransitionRangeId = Shader.PropertyToID("_HectonLodTransitionRange");
        private static readonly int _ImpostorWidthId = Shader.PropertyToID("_HectonImpostorWidth");
        private static readonly int _ImpostorHeightId = Shader.PropertyToID("_HectonImpostorHeight");
        private static readonly int _RuntimeLodParamsId = Shader.PropertyToID("_HectonVegetationRuntimeLodParams");
        private static readonly int _RuntimeDrawParamsId = Shader.PropertyToID("_HectonVegetationRuntimeDrawParams");
        private static readonly int _SourceInstanceCountId = Shader.PropertyToID("_HectonSourceInstanceCount");
        private static readonly int _ViewProjectionId = Shader.PropertyToID("_HectonViewProjection");
        private static readonly int _ViewMatrixId = Shader.PropertyToID("_HectonViewMatrix");
        private static readonly int _CameraPositionId = Shader.PropertyToID("_HectonCameraPosition");
        private static readonly int _CameraForwardId = Shader.PropertyToID("_HectonCameraForward");
        private static readonly int _CameraDepthTextureId = Shader.PropertyToID("_HectonCameraDepthTexture");
        private static readonly int _DepthPyramidTextureId = Shader.PropertyToID("_HectonDepthPyramid");
        private static readonly int _DepthPyramidMipCountId = Shader.PropertyToID("_HectonDepthPyramidMipCount");
        private static readonly int _DepthPyramidTexelSizeId = Shader.PropertyToID("_HectonDepthPyramidTexelSize");
        private static readonly int _FrustumPlanesId = Shader.PropertyToID("_HectonFrustumPlanes");
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
        private static readonly int _BiolumIntensityVectorId = Shader.PropertyToID("_BiolumIntensity");
        private static readonly int _PeripheralCullDotId = Shader.PropertyToID("_HectonPeripheralCullDot");
        private static readonly int _PeripheralCullDistanceSqId = Shader.PropertyToID("_HectonPeripheralCullDistanceSq");
        private static readonly int _SourceMatricesId = Shader.PropertyToID("_HectonSourceInstanceMatrices");
        private static readonly int _SourceDataId = Shader.PropertyToID("_HectonSourceVegetationInstanceData");
        private static readonly int _VisibleIndicesLod0Id = Shader.PropertyToID("_HectonVisibleInstanceIndicesLOD0");
        private static readonly int _VisibleIndicesLod1Id = Shader.PropertyToID("_HectonVisibleInstanceIndicesLOD1");
        private static readonly int _VisibleIndicesShadowId = Shader.PropertyToID("_HectonVisibleInstanceIndicesShadow");
        private static readonly int _FarLodAppendEnabledId = Shader.PropertyToID("_HectonFarLodAppendEnabled");
        private static readonly int _DensityDecimationStepId = Shader.PropertyToID("_HectonDensityDecimationStep");
        private static readonly int _CullTelemetryCountersId = Shader.PropertyToID("_HectonCullTelemetryCounters");
        private static readonly int _CullTelemetryEnabledId = Shader.PropertyToID("_HectonCullTelemetryEnabled");
        private static readonly int _IndirectArgsBufferId = Shader.PropertyToID("_HectonIndirectArgsBuffer");
        private static readonly int _IndirectIndexCountPerInstanceId = Shader.PropertyToID("_HectonIndirectIndexCountPerInstance");
        private static readonly int _IndirectStartIndexId = Shader.PropertyToID("_HectonIndirectStartIndex");
        private static readonly int _IndirectBaseVertexIndexId = Shader.PropertyToID("_HectonIndirectBaseVertexIndex");
        private static readonly int _PreviousCameraPositionId = Shader.PropertyToID("_HectonPreviousCameraPosition");
        private static readonly int _DepthPyramidSourceDepthId = Shader.PropertyToID("_HectonDepthPyramidSourceDepth");
        private static readonly int _DepthPyramidSourceId = Shader.PropertyToID("_HectonDepthPyramidSource");
        private static readonly int _DepthPyramidTargetId = Shader.PropertyToID("_HectonDepthPyramidTarget");
        private static readonly int _SubmarineWashSphereId = Shader.PropertyToID("_HectonSubmarineWashSphere");
        private static readonly int _SubmarineWashVelocityId = Shader.PropertyToID("_HectonSubmarineWashVelocity");
        private const int MaxScooterHeadlights = 2;

        [Header("Rendering")]
        [SerializeField]
        [Tooltip("Material that consumes the indirect vegetation matrix and metadata buffers in the shader.")]
        private Material _material;

        [SerializeField]
        [Tooltip("First-party vegetation shader reference retained for validation; runtime material creation is forbidden for this renderer.")]
        private Shader _vegetationShader;

        [SerializeField]
        [Tooltip("Compute shader that performs GPU frustum culling and per-instance LOD classification.")]
        private ComputeShader _cullingCompute;

        [SerializeField]
        [Tooltip("Abyssal flow compute shader kernel used to persist GPU-only snapped flora flags.")]
        private ComputeShader _abyssalFlowFieldCompute;

        [SerializeField]
        [Tooltip("Compute shader that builds the vegetation Hi-Z depth pyramid consumed by the culling kernel.")]
        private ComputeShader _depthPyramidCompute;

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

        #pragma warning disable 0414
        [SerializeField]
        [Tooltip("Legacy inspector field retained for serialized data compatibility after BRG migration.")]
        private ShadowCastingMode _shadowCastingMode = ShadowCastingMode.Off;

        [SerializeField]
        [Tooltip("Whether the near indirect vegetation draw call should receive shadows.")]
        private bool _receiveShadows;

        [SerializeField]
        [Tooltip("Legacy inspector field retained for serialized data compatibility after BRG migration.")]
        private ShadowCastingMode _impostorShadowCastingMode = ShadowCastingMode.Off;
        #pragma warning restore 0414

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
        private float _nearLodDistance = 20f;

        [SerializeField, Range(60f, 180f)]
        [Tooltip("Far band end distance in meters. Billboard cards render only up to this radius.")]
        private float _farLodDistance = 150f;

        [SerializeField, Range(0.5f, 20f)]
        [Tooltip("Cross-fade range around the near/far band thresholds. Runtime is locked to the 2m flora dither mandate.")]
        private float _lodTransitionRange = LodTransitionRangeMeters;

        [SerializeField, Range(1, 8)]
        [Tooltip("Far LOD GPU culling cadence. 4 means distant vegetation visibility refreshes at 15Hz on a 60Hz frame budget.")]
        private int _farCullingFrameStride = 4;

        [SerializeField, Min(0f)]
        [Tooltip("Far LOD cadence only engages when the far vegetation band extends beyond this distance in meters.")]
        private float _farCullingCadenceDistance = 50f;

        [Header("GPU Occlusion")]
        [SerializeField]
        [Tooltip("Uses a GPU indirect render path with append-buffer visibility lists and indirect argument buffers when the compute kernels are available.")]
        private bool _preferGpuIndirectRendering = true;

        #pragma warning disable 0414
        [SerializeField]
        [Tooltip("Legacy inspector field retained for serialized data compatibility after BRG migration.")]
        private bool _enableDepthOcclusion = true;

        [SerializeField, Range(0.05f, 2f)]
        [Tooltip("Legacy inspector field retained for serialized data compatibility after BRG migration.")]
        private float _occlusionDepthBias = 0.35f;
        #pragma warning restore 0414

        [Header("Darkness Culling")]
        [SerializeField]
        [Tooltip("Rejects flora instances that are outside the published scooter headlights and below the global biolum threshold.")]
        private bool _enableDarknessCulling = true;

        [SerializeField, Range(0.001f, 0.25f)]
        [Tooltip("Minimum combined global biolum scalar required to keep completely unlit instances alive.")]
        private float _darknessBiolumThreshold = 0.05f;

        [Header("Peripheral Cull")]
        [SerializeField, Range(-1f, 1f)]
        [Tooltip("When an instance falls below this camera-forward dot product and is beyond the peripheral distance, the GPU culling kernel rejects it.")]
        private float _peripheralCullDot = 0.5f;

        [SerializeField, Min(0f)]
        [Tooltip("Distance in meters after which peripheral instances become eligible for the dot-product cone cull.")]
        private float _peripheralCullDistance = 30f;

        [Header("Density Scaling")]
        [SerializeField, Range(0.05f, 1f)]
        [Tooltip("Upper density scalar applied by deterministic decimation. 0.5 keeps roughly every second candidate.")]
        private float _maxDensity01 = 1f;

        [SerializeField, Range(1, 4)]
        [Tooltip("Minimum deterministic density-decimation step. System health may raise this at runtime.")]
        private int _minimumDensityDecimationStep = 1;

        [SerializeField]
        [Tooltip("Samples GPU cull counters into a 300-frame native ring for the Scatter Diagnostics window.")]
        private bool _enableCullTelemetry = true;

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
        private GraphicsBuffer _instanceMatrixBuffer;
        private GraphicsBuffer _instanceDataBuffer;
        private GraphicsBuffer _floraPhaseSeedBuffer;
        private GraphicsBuffer _legacyInstanceDataBuffer;
        private GraphicsBuffer _uploadedInstanceMatrixBuffer;
        private GraphicsBuffer _uploadedInstanceDataBuffer;
        private IHectonIndirectVegetationBufferSource _bufferSource;
        private Bounds _explicitBounds;
        private bool _hasBoundsOverride;
        private bool _isRegistered;
        private bool _originShiftRegistered;
        private bool _legacyDataDirty = true;
        private int _instanceCount;
        private Camera _cachedCullCamera;
        [SerializeField]
        [Tooltip("Authored depth-only vegetation pass material. Runtime creation is forbidden for the biolum/vegetation pulse route.")]
        private Material _depthOnlyMaterial;

        [SerializeField]
        [Tooltip("Authored shadow-caster vegetation pass material. Runtime creation is forbidden for the biolum/vegetation pulse route.")]
        private Material _shadowCasterMaterial;

        [SerializeField]
        [Tooltip("Authored motion-vector vegetation pass material. Runtime creation is forbidden for the biolum/vegetation pulse route.")]
        private Material _motionVectorMaterial;
        private Vector3 _previousMotionCameraPosition;
        private Camera _previousMotionCamera;
        private bool _hasPreviousMotionCameraPosition;
        private Vector3 _cachedCullCameraPosition;
        private Vector3 _cachedCullCameraForward = Vector3.forward;
        private PlayerToolManager _playerToolManager;
        private float _nextToolManagerResolveTime;
        private BatchRendererGroup _batchRendererGroup;
        private NativeArray<MetadataValue> _batchMetadata;
        private GraphicsBuffer _batchHandleBuffer;
        private BatchID _batchId;
        private GraphicsBuffer _registeredBatchBuffer;
        private BatchMeshID _nearBatchMeshId;
        private BatchMeshID _farBatchMeshId;
        private Mesh _registeredNearMesh;
        private Mesh _registeredFarMesh;
        private BatchMaterialID _nearBatchMaterialId;
        private BatchMaterialID _farBatchMaterialId;
        private BatchMaterialID _depthNearBatchMaterialId;
        private BatchMaterialID _depthFarBatchMaterialId;
        private BatchMaterialID _shadowBatchMaterialId;
        private BatchMaterialID _motionNearBatchMaterialId;
        private BatchMaterialID _motionFarBatchMaterialId;
        private Material _registeredNearBrgMaterial;
        private Material _registeredFarBrgMaterial;
        private Material _registeredDepthNearBrgMaterial;
        private Material _registeredDepthFarBrgMaterial;
        private Material _registeredShadowBrgMaterial;
        private Material _registeredMotionNearBrgMaterial;
        private Material _registeredMotionFarBrgMaterial;
        private Material _nearBrgMaterial;
        private Material _farBrgMaterial;
        private Material _depthNearBrgMaterial;
        private Material _depthFarBrgMaterial;
        private Material _shadowBrgMaterial;
        private Material _motionNearBrgMaterial;
        private Material _motionFarBrgMaterial;
        private MaterialPropertyBlock _nearIndirectProperties;
        private MaterialPropertyBlock _farIndirectProperties;
        private MaterialPropertyBlock _depthNearIndirectProperties;
        private MaterialPropertyBlock _depthFarIndirectProperties;
        private MaterialPropertyBlock _shadowIndirectProperties;
        private MaterialPropertyBlock _motionNearIndirectProperties;
        private MaterialPropertyBlock _motionFarIndirectProperties;
        private MaterialBindingState _nearMaterialBindingState;
        private MaterialBindingState _farMaterialBindingState;
        private MaterialBindingState _depthNearMaterialBindingState;
        private MaterialBindingState _depthFarMaterialBindingState;
        private MaterialBindingState _shadowMaterialBindingState;
        private MaterialBindingState _motionNearMaterialBindingState;
        private MaterialBindingState _motionFarMaterialBindingState;
        private MaterialVectorBindingState _motionNearPreviousCameraBindingState;
        private MaterialVectorBindingState _motionFarPreviousCameraBindingState;
        private ComputeCullBindingState _mainCullComputeBindingState;
        private ComputeCullBindingState _shadowCullComputeBindingState;
        private ComputeSnapBindingState _clearSnapComputeBindingState;
        private ComputeSnapBindingState _flagSnapComputeBindingState;
        private IndirectArgsClearBindingState _indirectArgsClearBindingState;
        private NativeArray<Matrix4x4> _cpuCullingMatrices;
        private NativeArray<HectonVegetationInstanceData> _cpuCullingData;
        private CpuCullingScratchBuffer _cpuCullingScratchA;
        private CpuCullingScratchBuffer _cpuCullingScratchB;
        private int _cpuCullingScratchCursor;
        private JobHandle _cpuCullingDataDisposeHandle;
        private JobHandle _cpuCullingScratchDisposeHandle;
        private bool _cpuCullingDataDisposeHandleValid;
        private bool _cpuCullingScratchDisposeHandleValid;
        private bool _hasCpuCullingData;

        private Vector4[] _scooterHeadlightPositionsWs;
        private Vector4[] _scooterHeadlightDirectionsWs;
        private Vector4[] _scooterHeadlightColors;
        private Vector4[] _scooterHeadlightConeData;

        // COLD ALLOC: Camera[8] - camera discovery cache for GPU culling dispatch - owner: HectonIndirectVegetationRenderer
        private readonly Camera[] _cameraSearchCache = new Camera[8];
        private Plane[] _frustumPlaneCache;
        private Vector4[] _frustumPlaneVectors;
        private GraphicsBuffer _visibleIndicesLod0Buffer;
        private GraphicsBuffer _visibleIndicesLod1Buffer;
        private GraphicsBuffer _visibleIndicesShadowBuffer;
        private GraphicsBuffer _floraAgeBuffer;
        private GraphicsBuffer _floraSnapFlagBuffer;
        private GraphicsBuffer _indirectArgsLod0Buffer;
        private GraphicsBuffer _indirectArgsLod1Buffer;
        private GraphicsBuffer _indirectArgsShadowBuffer;
        private GraphicsBuffer _cullTelemetryCountersBuffer;
        private int _gpuVisibleIndexCapacity;
        private int _floraAgeCapacity;
        private int _floraSnapFlagCapacity;
        private bool _floraAgeBufferDirty = true;
        private bool _floraAgesAuthoredExternally;
        private bool _floraSnapFlagBufferRequiresClear;
        private int _gpuCullingFrameIndex;
        private bool _hasFarCullingSnapshot;
        private RenderTexture _depthPyramidTexture;
        private int _depthPyramidWidth;
        private int _depthPyramidHeight;
        private int _depthPyramidMipCount;
        private int _cullFloraKernel = -1;
        private int _cullFloraShadowKernel = -1;
        private int _clearIndirectArgsKernel = -1;
        private int _clearFloraSnapFlagsKernel = -1;
        private int _flagSnappedFloraKernel = -1;
        private int _depthPyramidCopyKernel = -1;
        private int _depthPyramidDownsampleKernel = -1;

        private HectonVegetationInstanceData[] _legacyInstanceData;
        private NativeArray<float> _floraAges01;
#if UNITY_EDITOR
        private NativeList<Matrix4x4> _mockScatterMatrices;
        private NativeList<HectonVegetationInstanceData> _mockScatterData;
        private const int EditorScatterGizmoBoundsCapacity = 96;
        private static readonly Bounds[] s_editorScatterVisibleBounds = new Bounds[EditorScatterGizmoBoundsCapacity]; // COLD ALLOC: Bounds[96] - SHINOBU_09 editor visible flora gizmo cache - owner: HectonIndirectVegetationRenderer
        private static readonly Bounds[] s_editorScatterCulledBounds = new Bounds[EditorScatterGizmoBoundsCapacity]; // COLD ALLOC: Bounds[96] - SHINOBU_09 editor culled flora gizmo cache - owner: HectonIndirectVegetationRenderer
        [SerializeField]
        private bool _drawEditorScatterDebugGizmos;
#endif
        private NativeArray<FloraGrowthTelemetryEntry> _floraGrowthTelemetry;
        private NativeArray<ScatterCullTelemetryEntry> _scatterCullTelemetry;
        private uint[] _cullTelemetryClearPayload;
        private int _floraGrowthTelemetryCursor;
        private int _lastFloraGrowthTelemetryFrame = -1;
        private int _scatterCullTelemetryCursor;
        private int _lastScatterCullTelemetryFrame = -1;
        private int _lastScatterCullTelemetrySampleFrame = -1;
        private int _resolvedDensityDecimationStep = 1;
        private byte _cachedScalabilityTierProfileByte = ScalabilityTierProfiles.HighRtx;
        private float _cachedSystemStress01;
        private AsyncGPUReadbackRequest _cullTelemetryReadbackRequest;
        private bool _floraGrowthTelemetryDumped;
        private bool _scatterCullTelemetryReadbackPending;
        private bool _scatterCullTelemetryDumped;
        private bool _lastCullOverdrawWarning;
        private const byte BindingFlagFalse = 0;
        private const byte BindingFlagTrue = 1;

        private static byte ToBindingFlag(bool value)
        {
            return value ? BindingFlagTrue : BindingFlagFalse;
        }

        private struct MaterialBindingState
        {
            public Material Material;
            public GraphicsBuffer InstanceMatrixBuffer;
            public GraphicsBuffer InstanceDataBuffer;
            public GraphicsBuffer FloraAgeBuffer;
            public GraphicsBuffer FloraPhaseSeedBuffer;
            public GraphicsBuffer FloraSnapFlagBuffer;
            public GraphicsBuffer VisibleIndicesBuffer;
            public Vector4 GlobalFloatingOffset;
            public float PassMode;
            public float NearDistance;
            public float FarDistance;
            public float TransitionRange;
            public float ImpostorWidth;
            public float ImpostorHeight;
            public byte UseGpuIndirectFlag;
            public byte IsValidFlag;
        }

        private struct MaterialVectorBindingState
        {
            public Material Material;
            public Vector3 Value;
            public byte IsValidFlag;
        }

        private struct ComputeCullBindingState
        {
            public ComputeShader Shader;
            public int Kernel;
            public GraphicsBuffer MatrixBuffer;
            public GraphicsBuffer InstanceDataBuffer;
            public GraphicsBuffer FloraAgeBuffer;
            public GraphicsBuffer VisibleLod0Buffer;
            public GraphicsBuffer VisibleLod1Buffer;
            public GraphicsBuffer VisibleShadowBuffer;
            public GraphicsBuffer TelemetryCountersBuffer;
            public byte IsShadowKernelFlag;
            public byte IsValidFlag;
        }

        private struct ComputeSnapBindingState
        {
            public ComputeShader Shader;
            public int Kernel;
            public GraphicsBuffer MatrixBuffer;
            public GraphicsBuffer InstanceDataBuffer;
            public GraphicsBuffer SnapFlagBuffer;
            public byte IsClearKernelFlag;
            public byte IsValidFlag;
        }

        private struct IndirectArgsClearBindingState
        {
            public ComputeShader Shader;
            public int Kernel;
            public GraphicsBuffer ArgsBuffer;
            public Mesh Mesh;
            public int SubMeshIndex;
            public int IndexCountPerInstance;
            public int StartIndex;
            public int BaseVertexIndex;
            public byte IsValidFlag;
        }

        private struct CpuCullingScratchBuffer
        {
            public NativeArray<byte> VisibilityMask;
            public NativeArray<float4> CullingPlanes;
            public NativeArray<float4> HeadlightPositionsWs;
            public NativeArray<float4> HeadlightDirectionsWs;
            public NativeArray<float4> HeadlightColors;
            public NativeArray<float4> HeadlightConeData;
            public JobHandle ActiveHandle;
            public int VisibilityCapacity;
            public byte ActiveHandleValidFlag;
        }

        [StructLayout(LayoutKind.Explicit, Size = 40)]
        private struct FloraGrowthTelemetryEntry
        {
            [FieldOffset(0)]
            public int FrameIndex;
            [FieldOffset(4)]
            public int InstanceCount;
            [FieldOffset(8)]
            public int SampleCount;
            [FieldOffset(12)]
            public int NegativeAgeCount;
            [FieldOffset(16)]
            public int NanAgeCount;
            [FieldOffset(20)]
            public int DirtyUpload;
            [FieldOffset(24)]
            public float MinAge01;
            [FieldOffset(28)]
            public float MaxAge01;
            [FieldOffset(32)]
            public uint AgeHash;
            [FieldOffset(36)]
            public int Reserved0;
        }

        [StructLayout(LayoutKind.Explicit, Size = 40)]
        public struct VegetationCullTelemetrySnapshot
        {
            [FieldOffset(0)]
            public int FrameIndex;
            [FieldOffset(4)]
            public int TotalInstances;
            [FieldOffset(8)]
            public int FrustumCulledCount;
            [FieldOffset(12)]
            public int OcclusionCulledCount;
            [FieldOffset(16)]
            public int VisibleCount;
            [FieldOffset(20)]
            public int DensityDecimationStep;
            [FieldOffset(24)]
            public int OverdrawWarning;
            [FieldOffset(28)]
            public float SystemStress01;
            [FieldOffset(32)]
            public float MaxDensity01;
            [FieldOffset(36)]
            public int Reserved0;
        }

        [StructLayout(LayoutKind.Explicit, Size = 40)]
        private struct ScatterCullTelemetryEntry
        {
            [FieldOffset(0)]
            public int FrameIndex;
            [FieldOffset(4)]
            public int TotalInstances;
            [FieldOffset(8)]
            public int FrustumCulledCount;
            [FieldOffset(12)]
            public int OcclusionCulledCount;
            [FieldOffset(16)]
            public int VisibleCount;
            [FieldOffset(20)]
            public int DensityDecimationStep;
            [FieldOffset(24)]
            public int OverdrawWarning;
            [FieldOffset(28)]
            public float SystemStress01;
            [FieldOffset(32)]
            public float MaxDensity01;
            [FieldOffset(36)]
            public int Reserved0;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct BuildVegetationVisibilityMaskJob : IJobParallelFor
        {
            [ReadOnly, NoAlias] public NativeArray<Matrix4x4> Matrices;
            [ReadOnly, NoAlias] public NativeArray<HectonVegetationInstanceData> InstanceData;
            [ReadOnly, NoAlias] public NativeArray<float4> CullingPlanes;
            [ReadOnly, NoAlias] public NativeArray<float4> HeadlightPositionsWs;
            [ReadOnly, NoAlias] public NativeArray<float4> HeadlightDirectionsWs;
            [ReadOnly, NoAlias] public NativeArray<float4> HeadlightColors;
            [ReadOnly, NoAlias] public NativeArray<float4> HeadlightConeData;
            [WriteOnly, NoAlias] public NativeArray<byte> VisibilityMask;
            public int InstanceCount;
            public int CullingPlaneCount;
            public int HeadlightCount;
            public byte EnableCpuCullingFlag;
            public byte UseFarPassFlag;
            public byte UseShadowPassFlag;
            public byte BypassDarknessCullingFlag;
            public int DensityDecimationStep;
            public float3 ViewPosition;
            public float3 GlobalOffset;
            public float Lod0MaxDistanceSq;
            public float Lod1MinDistanceSq;
            public float Lod1MaxDistanceSq;

            public void Execute(int index)
            {
                if (index >= InstanceCount)
                    return;

                if (!PassesDensityDecimation(index, DensityDecimationStep))
                {
                    VisibilityMask[index] = 0;
                    return;
                }

                byte instanceVisibility = 0;
                if (EnableCpuCullingFlag != 0)
                {
                    Matrix4x4 instanceMatrix = Matrices[index];
                    HectonVegetationInstanceData instanceData = InstanceData[index];
                    ResolveInstanceShape(instanceData, out float instanceHeight, out float instanceWidth);

                    float3 rootWs = TransformPoint(instanceMatrix, 0f, 0f, 0f) + GlobalOffset;
                    float3 centerWs = TransformPoint(instanceMatrix, 0f, instanceHeight * 0.5f, 0f) + GlobalOffset;
                    float3 topWs = TransformPoint(instanceMatrix, 0f, instanceHeight, 0f) + GlobalOffset;
                    float3 sideAWs = TransformPoint(instanceMatrix, instanceWidth, instanceHeight * 0.5f, 0f) + GlobalOffset;
                    float3 sideBWs = TransformPoint(instanceMatrix, -instanceWidth, instanceHeight * 0.5f, 0f) + GlobalOffset;

                    float radiusSq = math.max(
                        math.lengthsq(centerWs - rootWs),
                        math.max(
                            math.lengthsq(centerWs - topWs),
                            math.max(math.lengthsq(centerWs - sideAWs), math.lengthsq(centerWs - sideBWs))));
                    if (!IsSphereVisibleSq(centerWs, math.max(0.0625f, radiusSq)))
                    {
                        VisibilityMask[index] = 0;
                        return;
                    }

                    if (!IsVisibleInDarkness(centerWs))
                    {
                        VisibilityMask[index] = 0;
                        return;
                    }

                    float distanceSq = math.lengthsq(rootWs - ViewPosition);
                    if (distanceSq <= Lod0MaxDistanceSq)
                        instanceVisibility |= VisibilityMaskNear;

                    if (UseFarPassFlag != 0 && distanceSq >= Lod1MinDistanceSq && distanceSq <= Lod1MaxDistanceSq)
                        instanceVisibility |= VisibilityMaskFar;

                    if (UseShadowPassFlag != 0)
                        instanceVisibility |= VisibilityMaskShadow;
                }
                else
                {
                    instanceVisibility |= VisibilityMaskNear;
                    if (UseFarPassFlag != 0)
                        instanceVisibility |= VisibilityMaskFar;
                    if (UseShadowPassFlag != 0)
                        instanceVisibility |= VisibilityMaskShadow;
                }

                VisibilityMask[index] = instanceVisibility;
            }

            private bool IsVisibleInDarkness(float3 samplePositionWs)
            {
                if (BypassDarknessCullingFlag != 0)
                    return true;

                for (int headlightIndex = 0; headlightIndex < HeadlightCount; headlightIndex++)
                {
                    float4 lightPosition = HeadlightPositionsWs[headlightIndex];
                    float lightRange = math.max(0.1f, lightPosition.w);
                    float3 toSample = samplePositionWs - lightPosition.xyz;
                    float sampleDistanceSq = math.lengthsq(toSample);
                    float lightRangeSq = lightRange * lightRange;
                    if (sampleDistanceSq >= lightRangeSq || sampleDistanceSq <= 0.00000001f)
                        continue;

                    float4 directionData = HeadlightDirectionsWs[headlightIndex];
                    float3 lightDirection = directionData.xyz;
                    float outerCos = HeadlightConeData[headlightIndex].x;
                    float dotLight = math.dot(lightDirection, toSample);
                    if (!PassesDotThresholdSq(dotLight, outerCos, sampleDistanceSq))
                        continue;

                    float invRange = HeadlightConeData[headlightIndex].z;
                    float rangeAttenuation = math.saturate(1f - sampleDistanceSq * invRange * invRange);
                    rangeAttenuation *= rangeAttenuation;
                    float intensity = HeadlightColors[headlightIndex].w * HeadlightConeData[headlightIndex].y;
                    if (rangeAttenuation * intensity >= 0.02f)
                        return true;
                }

                return false;
            }

            private bool IsSphereVisibleSq(float3 center, float radiusSq)
            {
                for (int planeIndex = 0; planeIndex < CullingPlaneCount; planeIndex++)
                {
                    float4 plane = CullingPlanes[planeIndex];
                    float signedDistance = math.dot(plane.xyz, center) + plane.w;
                    if (signedDistance < 0f && signedDistance * signedDistance > radiusSq)
                        return false;
                }

                return true;
            }

            private static bool PassesDotThresholdSq(float dotValue, float threshold, float lengthProductSq)
            {
                if (!math.isfinite(dotValue) || !math.isfinite(threshold) || !math.isfinite(lengthProductSq) || lengthProductSq <= 0.00000001f)
                    return true;

                float thresholdSq = threshold * threshold;
                float dotSq = dotValue * dotValue;
                return threshold >= 0f
                    ? dotValue >= 0f && dotSq >= thresholdSq * lengthProductSq
                    : dotValue >= 0f || dotSq <= thresholdSq * lengthProductSq;
            }

            private static void ResolveInstanceShape(HectonVegetationInstanceData instanceData, out float instanceHeight, out float instanceWidth)
            {
                float instanceType = math.clamp(math.round(instanceData.Type), 0f, 2f);
                float encodedHeightScale = math.saturate(math.abs(instanceData.HeightScale));
                float encodedWidthScale = instanceData.WidthScale < 0f ? 1f : math.saturate(instanceData.WidthScale);
                if (instanceType < 0.5f)
                {
                    instanceHeight = math.lerp(0.35f, 1.4f, encodedHeightScale);
                    instanceWidth = math.lerp(0.65f, 1.25f, encodedWidthScale);
                    return;
                }

                if (instanceType < 1.5f)
                {
                    instanceHeight = math.lerp(10f, 20f, encodedHeightScale);
                    instanceWidth = math.lerp(0.55f, 1.6f, encodedWidthScale);
                    return;
                }

                instanceHeight = math.lerp(0.75f, 2.4f, encodedHeightScale);
                instanceWidth = math.lerp(0.75f, 1.35f, encodedWidthScale);
            }

            private static float3 TransformPoint(Matrix4x4 matrixValue, float x, float y, float z)
            {
                return new float3(
                    matrixValue.m00 * x + matrixValue.m01 * y + matrixValue.m02 * z + matrixValue.m03,
                    matrixValue.m10 * x + matrixValue.m11 * y + matrixValue.m12 * z + matrixValue.m13,
                    matrixValue.m20 * x + matrixValue.m21 * y + matrixValue.m22 * z + matrixValue.m23);
            }

            private static bool PassesDensityDecimation(int index, int decimationStep)
            {
                if (decimationStep <= 1)
                    return true;

                uint hash = Hash((uint)index * 747796405u + 2891336453u);
                return (hash % (uint)math.clamp(decimationStep, 1, 4)) == 0u;
            }

            private static uint Hash(uint value)
            {
                value ^= value >> 16;
                value *= 0x7feb352du;
                value ^= value >> 15;
                value *= 0x846ca68bu;
                value ^= value >> 16;
                return value;
            }
        }

#if UNITY_EDITOR
        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct MockMatrixGeneratorJob : IJobParallelFor
        {
            [NoAlias] public NativeArray<Matrix4x4> Matrices;
            [NoAlias] public NativeArray<HectonVegetationInstanceData> InstanceData;
            public int CellsX;
            public float Spacing;
            public uint Seed;

            public void Execute(int index)
            {
                int cellsX = math.max(1, CellsX);
                int x = index % cellsX;
                int z = index / cellsX;
                uint hash = Hash((uint)index ^ Seed);
                float jitterX = Hash01(hash ^ 0x9E3779B9u) - 0.5f;
                float jitterZ = Hash01(hash ^ 0x85EBCA6Bu) - 0.5f;
                float angle = Hash01(hash ^ 0xC2B2AE35u) * 6.2831855f;
                float height = math.lerp(0.35f, 1f, Hash01(hash ^ 0x27D4EB2Fu));
                float width = math.lerp(0.75f, 1f, Hash01(hash ^ 0x165667B1u));
                float scale = math.lerp(0.75f, 1.35f, Hash01(hash ^ 0xD3A2646Cu));
                float spacing = math.max(0.25f, Spacing);
                float originX = (x - (cellsX - 1) * 0.5f) * spacing + jitterX * spacing * 0.35f;
                float originZ = (z - (cellsX - 1) * 0.5f) * spacing + jitterZ * spacing * 0.35f;
                float sin = math.sin(angle);
                float cos = math.cos(angle);

                Matrix4x4 matrix = default;
                matrix.m00 = cos * scale;
                matrix.m01 = 0f;
                matrix.m02 = -sin * scale;
                matrix.m03 = originX;
                matrix.m10 = 0f;
                matrix.m11 = scale;
                matrix.m12 = 0f;
                matrix.m13 = 0f;
                matrix.m20 = sin * scale;
                matrix.m21 = 0f;
                matrix.m22 = cos * scale;
                matrix.m23 = originZ;
                matrix.m30 = 0f;
                matrix.m31 = 0f;
                matrix.m32 = 0f;
                matrix.m33 = 1f;
                Matrices[index] = matrix;

                float variation = Hash01(hash ^ 0xA24BAED5u);
                float type = variation < 0.55f ? 0f : (variation < 0.78f ? 1f : 2f);
                InstanceData[index] = new HectonVegetationInstanceData
                {
                    Type = type,
                    HeightScale = height,
                    WidthScale = width,
                    Variation = variation,
                    TemplateIndex = -1f,
                    RuntimeState = HectonVegetationInstanceData.RuntimeStateIdle,
                    RuntimeFlags = 0f,
                    PulseFrequency = 0.3f + variation,
                    BioluminescenceColor = new Vector4(0.05f, 0.35f + variation * 0.3f, 0.48f, 0.12f + variation * 0.2f),
                    SwaySpeed = 0.75f + variation * 0.75f,
                    BendAmplitude = 0.65f + variation * 0.55f,
                    HealthNormalized = 1f,
                    Reserved0 = 1f
                };
            }

            private static float Hash01(uint value)
            {
                return (Hash(value) & 0xFFFFu) * (1f / 65535f);
            }

            private static uint Hash(uint value)
            {
                value ^= value >> 16;
                value *= 0x7feb352du;
                value ^= value >> 15;
                value *= 0x846ca68bu;
                value ^= value >> 16;
                return value;
            }
        }
#endif

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private unsafe struct FinalizeVegetationDrawOutputJob : IJob
        {
            [ReadOnly, NoAlias] public NativeArray<byte> VisibilityMask;
            public int InstanceCount;
            public int Layer;
            public int SubMeshIndex;
            public byte UseFarPassFlag;
            public byte UseDepthPassFlag;
            public byte UseDepthFarPassFlag;
            public byte UseShadowPassFlag;
            public byte UseMotionPassFlag;
            public byte UseMotionFarPassFlag;
            public BatchID BatchId;
            public BatchMeshID NearMeshId;
            public BatchMeshID FarMeshId;
            public BatchMaterialID NearMaterialId;
            public BatchMaterialID FarMaterialId;
            public BatchMaterialID DepthNearMaterialId;
            public BatchMaterialID DepthFarMaterialId;
            public BatchMaterialID ShadowMaterialId;
            public BatchMaterialID MotionNearMaterialId;
            public BatchMaterialID MotionFarMaterialId;
            [NativeDisableUnsafePtrRestriction] public int* VisibleInstances;
            [NativeDisableUnsafePtrRestriction] public BatchDrawCommand* DrawCommands;
            [NativeDisableUnsafePtrRestriction] public BatchDrawRange* DrawRanges;
            [NativeDisableUnsafePtrRestriction] public BatchCullingOutputDrawCommands* OutputCommands;

            public void Execute()
            {
                int nearCount = 0;
                int farCount = 0;
                int shadowCount = 0;
                for (int instanceIndex = 0; instanceIndex < InstanceCount; instanceIndex++)
                {
                    byte instanceVisibility = VisibilityMask[instanceIndex];
                    if ((instanceVisibility & VisibilityMaskNear) != 0)
                        nearCount++;
                    if ((instanceVisibility & VisibilityMaskFar) != 0)
                        farCount++;
                    if ((instanceVisibility & VisibilityMaskShadow) != 0)
                        shadowCount++;
                }

                int nearOffset = 0;
                int farOffset = nearCount;
                int shadowOffset = nearCount + farCount;
                int nearWrite = 0;
                int farWrite = 0;
                int shadowWrite = 0;

                for (int instanceIndex = 0; instanceIndex < InstanceCount; instanceIndex++)
                {
                    byte instanceVisibility = VisibilityMask[instanceIndex];
                    if ((instanceVisibility & VisibilityMaskNear) != 0)
                    {
                        VisibleInstances[nearOffset + nearWrite] = instanceIndex;
                        nearWrite++;
                    }

                    if ((instanceVisibility & VisibilityMaskFar) != 0)
                    {
                        VisibleInstances[farOffset + farWrite] = instanceIndex;
                        farWrite++;
                    }

                    if ((instanceVisibility & VisibilityMaskShadow) != 0)
                    {
                        VisibleInstances[shadowOffset + shadowWrite] = instanceIndex;
                        shadowWrite++;
                    }
                }

                int commandIndex = 0;
                commandIndex = WriteVegetationDrawCommand(
                    commandIndex,
                    nearOffset,
                    nearWrite,
                    NearMaterialId,
                    NearMeshId,
                    ShadowCastingMode.Off,
                    false,
                    MotionVectorGenerationMode.Camera);

                if (UseFarPassFlag != 0)
                {
                    commandIndex = WriteVegetationDrawCommand(
                        commandIndex,
                        farOffset,
                        farWrite,
                        FarMaterialId,
                        FarMeshId,
                        ShadowCastingMode.Off,
                        false,
                        MotionVectorGenerationMode.Camera);
                }

                if (UseDepthPassFlag != 0)
                {
                    commandIndex = WriteVegetationDrawCommand(
                        commandIndex,
                        nearOffset,
                        nearWrite,
                        DepthNearMaterialId,
                        NearMeshId,
                        ShadowCastingMode.Off,
                        false,
                        MotionVectorGenerationMode.Camera);

                    if (UseDepthFarPassFlag != 0)
                    {
                        commandIndex = WriteVegetationDrawCommand(
                            commandIndex,
                            farOffset,
                            farWrite,
                            DepthFarMaterialId,
                            FarMeshId,
                            ShadowCastingMode.Off,
                            false,
                            MotionVectorGenerationMode.Camera);
                    }
                }

                if (UseShadowPassFlag != 0)
                {
                    commandIndex = WriteVegetationDrawCommand(
                        commandIndex,
                        shadowOffset,
                        shadowWrite,
                        ShadowMaterialId,
                        NearMeshId,
                        ShadowCastingMode.On,
                        false,
                        MotionVectorGenerationMode.Camera);
                }

                if (UseMotionPassFlag != 0)
                {
                    commandIndex = WriteVegetationDrawCommand(
                        commandIndex,
                        nearOffset,
                        nearWrite,
                        MotionNearMaterialId,
                        NearMeshId,
                        ShadowCastingMode.Off,
                        false,
                        MotionVectorGenerationMode.Object);

                    if (UseMotionFarPassFlag != 0)
                    {
                        commandIndex = WriteVegetationDrawCommand(
                            commandIndex,
                            farOffset,
                            farWrite,
                            MotionFarMaterialId,
                            FarMeshId,
                            ShadowCastingMode.Off,
                            false,
                            MotionVectorGenerationMode.Object);
                    }
                }

                *OutputCommands = new BatchCullingOutputDrawCommands
                {
                    visibleInstances = VisibleInstances,
                    visibleInstanceCount = nearWrite + farWrite + shadowWrite,
                    drawCommands = DrawCommands,
                    drawCommandCount = commandIndex,
                    drawRanges = DrawRanges,
                    drawRangeCount = commandIndex
                };
            }

            private int WriteVegetationDrawCommand(
                int commandIndex,
                int visibleOffset,
                int visibleCount,
                BatchMaterialID materialId,
                BatchMeshID meshId,
                ShadowCastingMode shadowCastingMode,
                bool receiveShadows,
                MotionVectorGenerationMode motionMode)
            {
                if (visibleCount <= 0 || materialId.value == 0u || meshId.value == 0u)
                    return commandIndex;

                DrawCommands[commandIndex] = new BatchDrawCommand
                {
                    flags = BatchDrawCommandFlags.None,
                    visibleOffset = (uint)visibleOffset,
                    visibleCount = (uint)visibleCount,
                    batchID = BatchId,
                    materialID = materialId,
                    splitVisibilityMask = ushort.MaxValue,
                    lightmapIndex = ushort.MaxValue,
                    sortingPosition = 0,
                    meshID = meshId,
                    submeshIndex = (ushort)math.max(0, SubMeshIndex)
                };
                DrawRanges[commandIndex] = new BatchDrawRange
                {
                    drawCommandsBegin = (uint)commandIndex,
                    drawCommandsCount = 1u,
                    drawCommandsType = BatchDrawCommandType.Direct,
                    filterSettings = new BatchFilterSettings
                    {
                        renderingLayerMask = HectonLayerMasks.AllDefinedProjectRenderingLayerMaskValue,
                        rendererPriority = 0,
                        layer = (byte)math.clamp(Layer, byte.MinValue, byte.MaxValue),
                        shadowCastingMode = shadowCastingMode,
                        receiveShadows = receiveShadows,
                        motionMode = motionMode,
                        staticShadowCaster = false,
                        allDepthSorted = false
                    }
                };
                return commandIndex + 1;
            }
        }

        /// <summary>True when an external matrix buffer is currently bound.</summary>
        public bool HasMatrixBuffer => _instanceMatrixBuffer != null;

        /// <summary>True when either an external or fallback instance metadata buffer is currently bound.</summary>
        public bool HasInstanceDataBuffer => _instanceDataBuffer != null || _legacyInstanceDataBuffer != null;

        /// <summary>Current active instance count published into the indirect args payload.</summary>
        public int BoundInstanceCount => _instanceCount;

        /// <summary>Read-only renderer-owned SoA growth lane uploaded as _HectonFloraAges01. Negative entries are harvested/culling sentinels.</summary>
        public NativeArray<float>.ReadOnly FloraAges01 => _floraAges01.IsCreated ? _floraAges01.AsReadOnly() : default;

        /// <summary>
        /// Writes one authored flora age into the renderer-owned SoA lane and schedules a GPU upload.
        /// </summary>
        /// <param name="instanceIndex">Active vegetation instance index.</param>
        /// <param name="age01">Growth age. Negative values mean harvested/culled. Non-finite values become the cull sentinel.</param>
        /// <returns>True when the age entry was accepted.</returns>
        public bool TrySetFloraAge01(int instanceIndex, float age01)
        {
            if (instanceIndex < 0 || instanceIndex >= _instanceCount)
                return false;

            EnsureFloraAgeCapacity(_instanceCount);
            if (!_floraAges01.IsCreated || instanceIndex >= _floraAges01.Length)
                return false;

            _floraAges01[instanceIndex] = SanitizeFloraAgeForUpload(age01);
            _floraAgesAuthoredExternally = true;
            _floraAgeBufferDirty = true;
            return true;
        }

        /// <summary>
        /// Marks renderer-owned flora age data for upload after an explicit owner-authorized write path.
        /// </summary>
        public void MarkFloraAgesDirty()
        {
            if (!_floraAges01.IsCreated)
                return;

            _floraAgesAuthoredExternally = true;
            _floraAgeBufferDirty = true;
        }

        /// <summary>
        /// Copies an external NativeArray age lane into the renderer-owned SoA buffer for deterministic restore or farming systems.
        /// </summary>
        /// <param name="ages01">Source age lane. Negative values are cull sentinels.</param>
        /// <param name="count">Number of entries to copy.</param>
        /// <returns>True when the source was valid and at least one active entry was copied.</returns>
        public bool TryCopyFloraAges01(NativeArray<float> ages01, int count)
        {
            if (!ages01.IsCreated || count <= 0 || ages01.Length < count || _instanceCount <= 0)
                return false;

            int copyCount = math.min(count, _instanceCount);
            EnsureFloraAgeCapacity(_instanceCount);
            if (!_floraAges01.IsCreated || _floraAges01.Length < copyCount)
                return false;

            for (int instanceIndex = 0; instanceIndex < copyCount; instanceIndex++)
                _floraAges01[instanceIndex] = SanitizeFloraAgeForUpload(ages01[instanceIndex]);

            _floraAgesAuthoredExternally = true;
            _floraAgeBufferDirty = true;
            return copyCount > 0;
        }

        /// <summary>Configured distance where full strip geometry stops rendering.</summary>
        public float NearLodDistance => _nearLodDistance;

        /// <summary>Configured distance where impostor rendering ends and the pass culls completely.</summary>
        public float FarLodDistance => _farLodDistance;

        /// <summary>Configured crossfade range around the LOD threshold.</summary>
        public float LodTransitionDistance => _lodTransitionRange;

        /// <summary>Maximum density scalar before deterministic hardware decimation is applied.</summary>
        public float MaxDensity01 => _maxDensity01;

        /// <summary>Designer-authored floor for deterministic density decimation.</summary>
        public int MinimumDensityDecimationStep => _minimumDensityDecimationStep;

        /// <summary>Resolved runtime decimation step after quality and SystemHealth pressure have been applied.</summary>
        public int ResolvedDensityDecimationStep => _resolvedDensityDecimationStep;

        /// <summary>Latest cached system stress scalar consumed by density decimation.</summary>
        public float SystemStress01 => _cachedSystemStress01;

        /// <summary>True when the last cull telemetry sample crossed the 50k visible-instance overdraw threshold.</summary>
        public bool CullOverdrawWarning => _lastCullOverdrawWarning;

        /// <summary>True when the far impostor pass is currently enabled.</summary>
        public bool UsesImpostorPass => _farLodDistance > _nearLodDistance;

        /// <summary>True when this renderer is currently consuming caller-provided array uploads staged into owned GPU buffers.</summary>
        public bool UsesOwnedUploadBuffers => _instanceMatrixBuffer == _uploadedInstanceMatrixBuffer;

        /// <summary>Approximate VRAM footprint in bytes for the renderer-owned graphics buffers.</summary>
        public long GetVRAMEstimation()
        {
            long totalBytes = 0L;
            totalBytes += EstimateGraphicsBufferBytes(_legacyInstanceDataBuffer);
            totalBytes += EstimateGraphicsBufferBytes(_uploadedInstanceMatrixBuffer);
            totalBytes += EstimateGraphicsBufferBytes(_uploadedInstanceDataBuffer);
            totalBytes += EstimateGraphicsBufferBytes(_batchHandleBuffer);
            totalBytes += EstimateGraphicsBufferBytes(_floraAgeBuffer);
            totalBytes += EstimateGraphicsBufferBytes(_floraSnapFlagBuffer);
            totalBytes += EstimateGraphicsBufferBytes(_cullTelemetryCountersBuffer);
            return totalBytes;
        }

        /// <summary>
        /// Writes live diagnostic LOD and density values. Editor tooling uses this instead of mutating serialized fields by reflection.
        /// </summary>
        public void SetDiagnosticLodAndDensity(float nearLodDistance, float farLodDistance, float maxDensity01)
        {
            SetDiagnosticScatterTuning(nearLodDistance, farLodDistance, maxDensity01, _minimumDensityDecimationStep);
        }

        /// <summary>
        /// Writes live diagnostic LOD and density values including the hardware-decimation floor.
        /// </summary>
        public void SetDiagnosticScatterTuning(float nearLodDistance, float farLodDistance, float maxDensity01, int minimumDensityDecimationStep)
        {
            _nearLodDistance = Mathf.Clamp(nearLodDistance, 1f, 500f);
            _farLodDistance = Mathf.Clamp(farLodDistance, _nearLodDistance, 1000f);
            _maxDensity01 = Mathf.Clamp(maxDensity01, 0.05f, 1f);
            _minimumDensityDecimationStep = Mathf.Clamp(minimumDensityDecimationStep, 1, 4);
            _resolvedDensityDecimationStep = ResolveDensityDecimationStep();
            _hasFarCullingSnapshot = false;
        }

        /// <summary>
        /// Returns the most recent 300-frame cull-ring sample without allocating.
        /// </summary>
        public bool TryGetLatestCullTelemetry(out VegetationCullTelemetrySnapshot snapshot)
        {
            snapshot = default;
            if (!_scatterCullTelemetry.IsCreated)
                return false;

            int readIndex = _scatterCullTelemetryCursor - 1;
            if (readIndex < 0)
                readIndex = ScatterCullTelemetryFrameCount - 1;

            ScatterCullTelemetryEntry entry = _scatterCullTelemetry[readIndex];
            if (entry.FrameIndex <= 0)
                return false;

            snapshot = new VegetationCullTelemetrySnapshot
            {
                FrameIndex = entry.FrameIndex,
                TotalInstances = entry.TotalInstances,
                FrustumCulledCount = entry.FrustumCulledCount,
                OcclusionCulledCount = entry.OcclusionCulledCount,
                VisibleCount = entry.VisibleCount,
                DensityDecimationStep = entry.DensityDecimationStep,
                OverdrawWarning = entry.OverdrawWarning,
                SystemStress01 = entry.SystemStress01,
                MaxDensity01 = entry.MaxDensity01
            };
            return true;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Builds a deterministic 100x100 no-producer scatter lane for BRG/compute validation.
        /// </summary>
        public bool GenerateMockScatterForDiagnostics()
        {
            return GenerateMockScatterForDiagnostics(MockScatterDefaultAxisCount, MockScatterDefaultAxisCount, MockScatterDefaultSpacing, MockScatterDefaultSeed);
        }

        /// <summary>
        /// Builds deterministic no-producer scatter matrices and metadata into persistent native lists, then binds them.
        /// </summary>
        public bool GenerateMockScatterForDiagnostics(int cellsX, int cellsZ, float spacing, uint seed)
        {
            int safeCellsX = Mathf.Clamp(cellsX, 1, 512);
            int safeCellsZ = Mathf.Clamp(cellsZ, 1, 512);
            int count = Mathf.Min(150000, safeCellsX * safeCellsZ);
            if (count <= 0)
                return false;

            _bufferSource = null;
            ReleaseMockScatterBuffers();
            _mockScatterMatrices = new NativeList<Matrix4x4>(count, DataVaultExemptVegetationMockScatterAllocator); // COLD ALLOC: NativeList<Matrix4x4>[mockCount] - SHINOBU_09 vacuum scatter matrices - owner: HectonIndirectVegetationRenderer
            _mockScatterData = new NativeList<HectonVegetationInstanceData>(count, DataVaultExemptVegetationMockScatterAllocator); // COLD ALLOC: NativeList<HectonVegetationInstanceData>[mockCount] - SHINOBU_09 vacuum scatter metadata - owner: HectonIndirectVegetationRenderer
            _mockScatterMatrices.ResizeUninitialized(count);
            _mockScatterData.ResizeUninitialized(count);
            NativeMemorySentinel.RegisterNativeList(_mockScatterMatrices, nameof(HectonIndirectVegetationRenderer), nameof(_mockScatterMatrices), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeList(_mockScatterData, nameof(HectonIndirectVegetationRenderer), nameof(_mockScatterData), NativeAllocationLifetime.Session);

            NativeArray<Matrix4x4> matrices = _mockScatterMatrices.AsArray();
            NativeArray<HectonVegetationInstanceData> instanceData = _mockScatterData.AsArray();
            MockMatrixGeneratorJob job = new MockMatrixGeneratorJob
            {
                Matrices = matrices,
                InstanceData = instanceData,
                CellsX = safeCellsX,
                Spacing = Mathf.Max(0.25f, spacing),
                Seed = seed
            };
            for (int i = 0; i < count; i++)
                job.Execute(i);

            float width = (safeCellsX + 2) * Mathf.Max(0.25f, spacing);
            float depth = (safeCellsZ + 2) * Mathf.Max(0.25f, spacing);
            SetDrawBounds(new Bounds(transform.position + _boundsCenterOffset, new Vector3(width, Mathf.Max(_boundsSize.y, 32f), depth)));
            return BindInstanceNativeArrays(matrices, instanceData, count);
        }

        /// <summary>True when editor gizmos should render sampled BRG scatter bounds.</summary>
        public bool EditorScatterDebugGizmosEnabled => _drawEditorScatterDebugGizmos;

        /// <summary>Enables the editor-only OnDrawGizmos scatter bounds hook.</summary>
        public void SetEditorScatterDebugGizmosEnabled(bool enabled)
        {
            _drawEditorScatterDebugGizmos = enabled;
        }

        /// <summary>
        /// Copies sampled per-instance debug bounds into caller-owned arrays for editor scene visualization.
        /// </summary>
        public int CopyDebugBoundsNonAlloc(Bounds[] visibleBounds, Bounds[] culledBounds)
        {
            if (!_hasCpuCullingData ||
                !_cpuCullingMatrices.IsCreated ||
                !_cpuCullingData.IsCreated ||
                _instanceCount <= 0)
            {
                return 0;
            }

            int capacity = Mathf.Min(
                visibleBounds != null ? visibleBounds.Length : 0,
                culledBounds != null ? culledBounds.Length : 0);
            if (capacity <= 0)
                return 0;

            int written = 0;
            int stride = Mathf.Max(1, _instanceCount / capacity);
            Vector3 viewPosition = _cachedCullCameraPosition;
            float farDistance = Mathf.Max(_farLodDistance, _nearLodDistance);
            float farDistanceSq = farDistance * farDistance;
            for (int instanceIndex = 0; instanceIndex < _instanceCount && written < capacity; instanceIndex += stride)
            {
                Matrix4x4 matrix = _cpuCullingMatrices[instanceIndex];
                HectonVegetationInstanceData data = _cpuCullingData[instanceIndex];
                ResolveInstanceShape(in data, out float instanceHeight, out float instanceWidth);
                Vector3 root = TransformPoint(matrix, 0f, 0f, 0f);
                Vector3 center = TransformPoint(matrix, 0f, instanceHeight * 0.5f, 0f);
                Vector3 size = new Vector3(
                    Mathf.Max(0.1f, instanceWidth * 2f),
                    Mathf.Max(0.1f, instanceHeight),
                    Mathf.Max(0.1f, instanceWidth * 2f));
                Bounds bounds = new Bounds(center, size);
                bool visible = (root - viewPosition).sqrMagnitude <= farDistanceSq;
                if (visible)
                {
                    visibleBounds[written] = bounds;
                    culledBounds[written] = default;
                }
                else
                {
                    visibleBounds[written] = default;
                    culledBounds[written] = bounds;
                }
                written++;
            }

            return written;
        }

        private void OnDrawGizmos()
        {
            if (!_drawEditorScatterDebugGizmos)
                return;

            int count = CopyDebugBoundsNonAlloc(s_editorScatterVisibleBounds, s_editorScatterCulledBounds);
            Color previousColor = Gizmos.color;
            for (int i = 0; i < count; i++)
            {
                Bounds visibleBounds = s_editorScatterVisibleBounds[i];
                if (visibleBounds.size.sqrMagnitude > 0.0001f)
                {
                    Gizmos.color = new Color(1f, 0.9f, 0.12f, 0.85f);
                    Gizmos.DrawWireCube(visibleBounds.center, visibleBounds.size);
                }

                Bounds culledBounds = s_editorScatterCulledBounds[i];
                if (culledBounds.size.sqrMagnitude > 0.0001f)
                {
                    Gizmos.color = new Color(1f, 0.08f, 0.05f, 0.65f);
                    Gizmos.DrawWireCube(culledBounds.center, culledBounds.size);
                }
            }

            Gizmos.color = previousColor;
        }
#endif

        private void Awake()
        {
            _nearLodDistance = Mathf.Max(1f, _nearLodDistance);
            _farLodDistance = Mathf.Max(_nearLodDistance, _farLodDistance);
            _lodTransitionRange = LodTransitionRangeMeters;
            _farCullingFrameStride = Mathf.Clamp(_farCullingFrameStride, 1, 8);
            _farCullingCadenceDistance = Mathf.Max(0f, _farCullingCadenceDistance);
            _maxDensity01 = Mathf.Clamp(_maxDensity01, 0.05f, 1f);
            _minimumDensityDecimationStep = Mathf.Clamp(_minimumDensityDecimationStep, 1, 4);
            _cachedScalabilityTierProfileByte = ScalabilityTierProfiles.Normalize(GlobalRegistry.ScalabilityTierProfileByte);
            _cachedSystemStress01 = 0f;
            _resolvedDensityDecimationStep = ResolveDensityDecimationStep();
            TryAutoAssignAssets();
            if (_cullingCompute != null)
            {
                _cullFloraKernel = _cullingCompute.FindKernel("CullFloraInstances");
                _cullFloraShadowKernel = _cullingCompute.FindKernel("CullFloraShadowInstances");
                _clearIndirectArgsKernel = _cullingCompute.FindKernel("ClearIndirectArgs");
            }
            if (_abyssalFlowFieldCompute != null)
            {
                _clearFloraSnapFlagsKernel = _abyssalFlowFieldCompute.FindKernel("ClearFloraSnapFlags");
                _flagSnappedFloraKernel = _abyssalFlowFieldCompute.FindKernel("FlagSnappedFlora");
            }
            if (_depthPyramidCompute != null)
            {
                _depthPyramidCopyKernel = _depthPyramidCompute.FindKernel("CopyDepthPyramidMip0");
                _depthPyramidDownsampleKernel = _depthPyramidCompute.FindKernel("DownsampleDepthPyramidMip");
            }

            if (!EnsureRenderMaterialResolved())
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError("[HectonIndirectVegetationRenderer] Material is required and fallback shader resolution failed.", this);
#endif
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError("[HectonIndirectVegetationRenderer] No near render mesh resolved.", this);
#endif
                enabled = false;
                return;
            }

            // COLD ALLOC: Vector4[2] - scooter headlight world-position payload cache for BRG darkness culling - owner: HectonIndirectVegetationRenderer
            _scooterHeadlightPositionsWs = new Vector4[MaxScooterHeadlights];
            // COLD ALLOC: Vector4[2] - scooter headlight direction payload cache for BRG darkness culling - owner: HectonIndirectVegetationRenderer
            _scooterHeadlightDirectionsWs = new Vector4[MaxScooterHeadlights];
            // COLD ALLOC: Vector4[2] - scooter headlight color/intensity payload cache for BRG darkness culling - owner: HectonIndirectVegetationRenderer
            _scooterHeadlightColors = new Vector4[MaxScooterHeadlights];
            // COLD ALLOC: Vector4[2] - scooter headlight cone payload cache for BRG darkness culling - owner: HectonIndirectVegetationRenderer
            _scooterHeadlightConeData = new Vector4[MaxScooterHeadlights];
            _frustumPlaneCache = new Plane[FrustumPlaneCount]; // COLD ALLOC: Plane[6] - cached frustum planes for GPU vegetation culling upload - owner: HectonIndirectVegetationRenderer
            _frustumPlaneVectors = new Vector4[FrustumPlaneCount]; // COLD ALLOC: Vector4[6] - packed frustum planes for compute upload - owner: HectonIndirectVegetationRenderer
            _cullTelemetryClearPayload = new uint[ScatterCullTelemetryCounterCount]; // COLD ALLOC: uint[4] - GPU cull telemetry counter clear payload - owner: HectonIndirectVegetationRenderer
            EnsureIndirectPropertyBlocks();
            CreateAuxiliaryMaterials();
        }

        private void OnEnable()
        {
            TryRegister();
            TryRegisterOriginShiftListener();
        }

        private void OnDisable()
        {
            TryUnregisterOriginShiftListener();
            TryUnregister();
            _hasPreviousMotionCameraPosition = false;
            _previousMotionCamera = null;
            ReleaseBatchRendererGroupResources();
            ReleaseGpuIndirectResources();
        }

        private void OnDestroy()
        {
            TryUnregisterOriginShiftListener();
            TryUnregister();
            ReleaseBatchRendererGroupResources();
            ReleaseGpuIndirectResources();
            ReleaseLegacyInstanceDataBuffer();
            ReleaseUploadedInstanceBuffers();
            ReleaseFloraAgeResources();
            ReleaseFloraGrowthTelemetryResources();
            ReleaseScatterCullTelemetryResources();
#if UNITY_EDITOR
            ReleaseMockScatterBuffers();
#endif
            ReleaseAuxiliaryMaterials();
            ReleaseCpuCullingData();
            ReleaseCpuCullingScratchBuffers(deferActiveJobs: true);

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
        public void BindInstanceBuffer(GraphicsBuffer instanceMatrixBuffer, int instanceCount)
        {
            _bufferSource = null;

            if (instanceMatrixBuffer == null || instanceCount <= 0 || instanceMatrixBuffer.count <= 0)
            {
                ClearInstanceBuffer();
                return;
            }

            InvalidateRenderStateForBufferIdentityChange(instanceMatrixBuffer, _instanceDataBuffer, _floraPhaseSeedBuffer);
            _instanceMatrixBuffer = instanceMatrixBuffer;
            _legacyDataDirty = true;
            _hasCpuCullingData = false;
            SetInstanceCount(instanceCount);
        }

        /// <summary>
        /// Binds the external per-instance metadata buffer populated by another system.
        /// </summary>
        /// <param name="instanceDataBuffer">Structured buffer of <see cref="HectonVegetationInstanceData"/> payloads.</param>
        public void BindInstanceDataBuffer(GraphicsBuffer instanceDataBuffer)
        {
            _bufferSource = null;

            if (instanceDataBuffer == null || instanceDataBuffer.count <= 0)
            {
                ClearInstanceDataBuffer();
                return;
            }

            InvalidateRenderStateForBufferIdentityChange(_instanceMatrixBuffer, instanceDataBuffer, _floraPhaseSeedBuffer);
            _instanceDataBuffer = instanceDataBuffer;
        }

        /// <summary>
        /// Binds the parallel per-instance cascade phase-seed buffer consumed by reactive flora shaders.
        /// </summary>
        /// <param name="floraPhaseSeedBuffer">Structured buffer containing one phase seed per active vegetation instance.</param>
        public void BindFloraPhaseSeedBuffer(GraphicsBuffer floraPhaseSeedBuffer)
        {
            GraphicsBuffer resolvedPhaseSeedBuffer = floraPhaseSeedBuffer != null && floraPhaseSeedBuffer.count > 0
                ? floraPhaseSeedBuffer
                : null;
            InvalidateRenderStateForBufferIdentityChange(_instanceMatrixBuffer, _instanceDataBuffer, resolvedPhaseSeedBuffer);
            _floraPhaseSeedBuffer = resolvedPhaseSeedBuffer;
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

            GraphicsBufferUploadUtility.UploadArray(_uploadedInstanceMatrixBuffer, instanceMatrices, instanceCount);
            _instanceMatrixBuffer = _uploadedInstanceMatrixBuffer;
            CopyCpuCullingPayload(instanceMatrices, instanceData, instanceCount);

            if (instanceData != null)
            {
                if (instanceData.Length < instanceCount || _uploadedInstanceDataBuffer == null)
                {
                    ClearInstanceBuffer();
                    return;
                }

                GraphicsBufferUploadUtility.UploadArray(_uploadedInstanceDataBuffer, instanceData, instanceCount);
                _instanceDataBuffer = _uploadedInstanceDataBuffer;
                _legacyDataDirty = false;
            }
            else
            {
                _instanceDataBuffer = null;
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

            InvalidateRenderStateForBufferIdentityChange(_uploadedInstanceMatrixBuffer, _uploadedInstanceDataBuffer, _floraPhaseSeedBuffer);
            GraphicsBufferUploadUtility.UploadNativeArray(_uploadedInstanceMatrixBuffer, instanceMatrices, instanceCount);
            GraphicsBufferUploadUtility.UploadNativeArray(_uploadedInstanceDataBuffer, instanceData, instanceCount);
            _instanceMatrixBuffer = _uploadedInstanceMatrixBuffer;
            _instanceDataBuffer = _uploadedInstanceDataBuffer;
            _legacyDataDirty = false;
            CopyCpuCullingPayload(instanceMatrices, instanceData, instanceCount);
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
            _floraPhaseSeedBuffer = null;
            _legacyDataDirty = true;
            _floraAgesAuthoredExternally = false;
            _floraAgeBufferDirty = true;
            _floraSnapFlagBufferRequiresClear = true;
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
            _legacyDataDirty = true;
            _floraAgeBufferDirty = true;
            if (clampedCount == 0)
                _floraAgesAuthoredExternally = false;
            _floraSnapFlagBufferRequiresClear = true;
            _hasFarCullingSnapshot = false;
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

        /// <inheritdoc />
        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            Vector3 shiftOffset = shiftData.ShiftOffset;
            if (shiftOffset.sqrMagnitude <= 0.000001f)
                return;

            _cachedCullCameraPosition -= shiftOffset;
            if (_hasPreviousMotionCameraPosition)
                _previousMotionCameraPosition -= shiftOffset;

            _hasPreviousMotionCameraPosition = false;
            _previousMotionCamera = null;
            _hasFarCullingSnapshot = false;
            _gpuCullingFrameIndex = 0;

            if (_hasBoundsOverride)
                _explicitBounds.center -= shiftOffset;
        }

        /// <summary>
        /// Executes the BRG-backed vegetation submission.
        /// </summary>
        /// <param name="deltaTime">Unused current frame delta required by ITickable.</param>
        public void Tick(float deltaTime)
        {
            SyncSourceBinding();
            ConsumeScatterRuntimeSignals();
            PollCullTelemetryReadback();

            Material renderMaterial = ResolveRenderMaterial();
            if (_instanceMatrixBuffer == null || _instanceCount <= 0 || renderMaterial == null)
                return;

            Mesh nearMesh = ResolveNearRenderMesh();
            if (nearMesh == null)
                return;

            Camera cullCamera = _cameraOverride != null ? _cameraOverride : ResolveCullCamera();
            Vector3 cullCameraPosition = _cachedCullCameraPosition;
            Vector3 cullCameraForward = _cachedCullCameraForward;
            if (cullCamera != null)
            {
                Transform cullTransform = cullCamera.transform;
                ResolveCullCameraPose(cullTransform, out cullCameraPosition, out cullCameraForward);
                _cachedCullCameraPosition = cullCameraPosition;
                _cachedCullCameraForward = cullCameraForward;
            }

            CreateAuxiliaryMaterials();
            Mesh farMesh = FrameTimeWatchdog.IsDistantFloraRenderingEnabled && _farLodDistance > _nearLodDistance
                ? ResolveImpostorRenderMesh()
                : null;
            Vector3 rendererPosition = ResolveRendererRuntimePosition();
            Bounds drawBounds = ResolveDrawBounds(rendererPosition);
            if (TryRenderGpuIndirect(cullCamera, nearMesh, farMesh, cullCameraPosition, cullCameraForward, drawBounds))
                return;

            ReleaseBatchRendererGroupResources();
        }

        private void ConsumeScatterRuntimeSignals()
        {
            ReadOnlySpan<ScalabilityChangedEvent> scalabilitySignals = SignalBus<ScalabilityChangedEvent>.GetFrameSnapshot();
            for (int signalIndex = 0; signalIndex < scalabilitySignals.Length; signalIndex++)
                _cachedScalabilityTierProfileByte = ScalabilityTierProfiles.Normalize(scalabilitySignals[signalIndex].CurrentTier);

            float stress01 = 0f;
            ReadOnlySpan<SystemHealthSignal> healthSignals = SignalBus<SystemHealthSignal>.GetFrameSnapshot();
            for (int signalIndex = 0; signalIndex < healthSignals.Length; signalIndex++)
            {
                SystemHealthSignal signal = healthSignals[signalIndex];
                if (math.isfinite(signal.SystemHealthIndex01))
                    stress01 = math.max(stress01, 1f - math.saturate(signal.SystemHealthIndex01));
                stress01 = math.max(stress01, math.saturate(signal.PressureLevel * 0.25f));
            }

            if (healthSignals.Length == 0)
                stress01 = _cachedSystemStress01;

            _cachedSystemStress01 = math.saturate(stress01);
            _resolvedDensityDecimationStep = ResolveDensityDecimationStep();
        }

        private int ResolveDensityDecimationStep()
        {
            int step = Mathf.Clamp(_minimumDensityDecimationStep, 1, 4);
            float maxDensity = Mathf.Clamp(_maxDensity01, 0.05f, 1f);
            if (maxDensity < 0.999f)
                step = Mathf.Max(step, Mathf.CeilToInt(1f / maxDensity));

            if (_cachedScalabilityTierProfileByte == ScalabilityTierProfiles.LowMx350)
                step = Mathf.Max(step, 2);

            if (_cachedSystemStress01 >= 0.85f)
                step = Mathf.Max(step, 3);
            else if (_cachedSystemStress01 >= 0.70f)
                step = Mathf.Max(step, 2);

            return Mathf.Clamp(step, 1, 4);
        }

        private static void ResolveCullCameraPose(Transform cullTransform, out Vector3 runtimePosition, out Vector3 forward)
        {
            if (cullTransform == null)
            {
                runtimePosition = Vector3.zero;
                forward = Vector3.forward;
                return;
            }

            runtimePosition = cullTransform.position;
            forward = cullTransform.forward;
        }

        private Vector3 ResolveRendererRuntimePosition()
        {
            return transform.position;
        }

        private Bounds ResolveDrawBounds(Vector3 rendererPosition)
        {
            return _hasBoundsOverride
                ? _explicitBounds
                : new Bounds(rendererPosition + _boundsCenterOffset, _boundsSize);
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

        private Material ResolveRenderMaterial()
        {
            if (!EnsureRenderMaterialResolved())
                return null;

            if (_material != null)
                return _material;

            return null;
        }

        private bool EnsureRenderMaterialResolved()
        {
            if (_material != null)
                return true;

#if UNITY_EDITOR
            TryAutoAssignAssets();
#endif

            return _material != null;
        }

        private void EnsureBatchRendererGroupResources()
        {
            if (_batchRendererGroup != null)
                return;

            _batchRendererGroup = new BatchRendererGroup(new BatchRendererGroupCreateInfo
            {
                cullingCallback = OnPerformCulling,
                userContext = IntPtr.Zero
            });

            _batchMetadata = new NativeArray<MetadataValue>(BrgMetadataPlaceholderCount, DataVaultExemptVegetationBrgMetadataAllocator); // COLD ALLOC: NativeArray<MetadataValue>[1] - BRG metadata placeholder for vegetation renderer - owner: HectonIndirectVegetationRenderer
            NativeMemorySentinel.RegisterNativeArray(_batchMetadata, nameof(HectonIndirectVegetationRenderer), nameof(_batchMetadata), NativeAllocationLifetime.Session);
            _batchHandleBuffer = HectonBatchRendererGroupUtility.CreateBatchHandleBuffer(); // COLD ALLOC: GraphicsBuffer[1] - BRG registration handle buffer for vegetation renderer - owner: HectonIndirectVegetationRenderer
            _batchId = _batchRendererGroup.AddBatch(_batchMetadata, _batchHandleBuffer.bufferHandle);
        }

        private bool TryBindGpuIndirectMaterials(GraphicsBuffer activeInstanceDataBuffer, Mesh farMesh)
        {
            if (activeInstanceDataBuffer == null ||
                _visibleIndicesLod0Buffer == null ||
                (farMesh != null && _visibleIndicesLod1Buffer == null) ||
                (_enableShadowCasterDraw && _visibleIndicesShadowBuffer == null))
            {
                return false;
            }

            Material sourceMaterial = ResolveRenderMaterial();
            if (sourceMaterial == null)
                return false;

            EnsureIndirectPropertyBlocks();
            _nearBrgMaterial = sourceMaterial;
            if (_nearBrgMaterial == null)
                return false;

            if (farMesh != null)
                _farBrgMaterial = sourceMaterial;
            else
                ClearPassMaterialReference(ref _farBrgMaterial);

            if (_enableDepthPrepass && _depthOnlyMaterial != null)
            {
                _depthNearBrgMaterial = _depthOnlyMaterial;
                if (farMesh != null)
                    _depthFarBrgMaterial = _depthOnlyMaterial;
                else
                    ClearPassMaterialReference(ref _depthFarBrgMaterial);
            }
            else
            {
                ClearPassMaterialReference(ref _depthNearBrgMaterial);
                ClearPassMaterialReference(ref _depthFarBrgMaterial);
            }

            if (_enableShadowCasterDraw && _shadowCasterMaterial != null)
                _shadowBrgMaterial = _shadowCasterMaterial;
            else
                ClearPassMaterialReference(ref _shadowBrgMaterial);

            if (_enableMotionVectorDraw && _motionVectorMaterial != null)
            {
                _motionNearBrgMaterial = _motionVectorMaterial;
                if (farMesh != null)
                    _motionFarBrgMaterial = _motionVectorMaterial;
                else
                    ClearPassMaterialReference(ref _motionFarBrgMaterial);
            }
            else
            {
                ClearPassMaterialReference(ref _motionNearBrgMaterial);
                ClearPassMaterialReference(ref _motionFarBrgMaterial);
            }

            Vector4 globalFloatingOffset = ResolveVegetationFloatingOffset();
            ApplyIndirectPropertyBlockBindings(ref _nearIndirectProperties, ref _nearMaterialBindingState, _nearBrgMaterial, activeInstanceDataBuffer, globalFloatingOffset, 0f, _visibleIndicesLod0Buffer, true);
            ApplyIndirectPropertyBlockBindings(ref _farIndirectProperties, ref _farMaterialBindingState, _farBrgMaterial, activeInstanceDataBuffer, globalFloatingOffset, 1f, _visibleIndicesLod1Buffer, true);
            ApplyIndirectPropertyBlockBindings(ref _depthNearIndirectProperties, ref _depthNearMaterialBindingState, _depthNearBrgMaterial, activeInstanceDataBuffer, globalFloatingOffset, 0f, _visibleIndicesLod0Buffer, true);
            ApplyIndirectPropertyBlockBindings(ref _depthFarIndirectProperties, ref _depthFarMaterialBindingState, _depthFarBrgMaterial, activeInstanceDataBuffer, globalFloatingOffset, 1f, _visibleIndicesLod1Buffer, true);
            ApplyIndirectPropertyBlockBindings(ref _shadowIndirectProperties, ref _shadowMaterialBindingState, _shadowBrgMaterial, activeInstanceDataBuffer, globalFloatingOffset, 0f, _visibleIndicesShadowBuffer, true);
            ApplyIndirectPropertyBlockBindings(ref _motionNearIndirectProperties, ref _motionNearMaterialBindingState, _motionNearBrgMaterial, activeInstanceDataBuffer, globalFloatingOffset, 0f, _visibleIndicesLod0Buffer, true);
            ApplyIndirectPropertyBlockBindings(ref _motionFarIndirectProperties, ref _motionFarMaterialBindingState, _motionFarBrgMaterial, activeInstanceDataBuffer, globalFloatingOffset, 1f, _visibleIndicesLod1Buffer, true);
            return true;
        }

        private void ApplyIndirectPropertyBlockBindings(
            ref MaterialPropertyBlock propertyBlock,
            ref MaterialBindingState state,
            Material material,
            GraphicsBuffer activeInstanceDataBuffer,
            Vector4 globalFloatingOffset,
            float passMode,
            GraphicsBuffer visibleIndicesBuffer,
            bool useGpuIndirect)
        {
            if (material == null || propertyBlock == null || _instanceMatrixBuffer == null || activeInstanceDataBuffer == null)
            {
                state = default;
                return;
            }

            GraphicsBuffer floraAgeBuffer = ResolveFloraAgeBuffer();
            if (MaterialBindingStateMatches(
                    in state,
                    material,
                    activeInstanceDataBuffer,
                    floraAgeBuffer,
                    globalFloatingOffset,
                    passMode,
                    visibleIndicesBuffer,
                    useGpuIndirect))
            {
                return;
            }

            propertyBlock.Clear();
            propertyBlock.SetBuffer(_InstanceMatricesId, _instanceMatrixBuffer);
            propertyBlock.SetBuffer(_InstanceDataId, activeInstanceDataBuffer);
            if (floraAgeBuffer != null)
                propertyBlock.SetBuffer(_FloraAges01Id, floraAgeBuffer);
            if (_floraPhaseSeedBuffer != null)
                propertyBlock.SetBuffer(_FloraPhaseSeedsId, _floraPhaseSeedBuffer);
            if (_floraSnapFlagBuffer != null)
                propertyBlock.SetBuffer(_FloraSnapFlagsId, _floraSnapFlagBuffer);
            if (useGpuIndirect && visibleIndicesBuffer != null)
                propertyBlock.SetBuffer(_VisibleInstanceIndicesId, visibleIndicesBuffer);

            propertyBlock.SetVector(_GlobalFloatingOffsetId, globalFloatingOffset);
            propertyBlock.SetVector(_ChunkWorldOffsetId, globalFloatingOffset);
            float snapFlagsEnabled = _floraSnapFlagBuffer != null ? 1f : 0f;
            propertyBlock.SetVector(_RuntimeLodParamsId, new Vector4(passMode, _nearLodDistance, _farLodDistance, _lodTransitionRange));
            propertyBlock.SetVector(_RuntimeDrawParamsId, new Vector4(snapFlagsEnabled, _impostorWidth, _impostorHeight, useGpuIndirect && visibleIndicesBuffer != null ? 1f : 0f));

            state = new MaterialBindingState
            {
                Material = material,
                InstanceMatrixBuffer = _instanceMatrixBuffer,
                InstanceDataBuffer = activeInstanceDataBuffer,
                FloraAgeBuffer = floraAgeBuffer,
                FloraPhaseSeedBuffer = _floraPhaseSeedBuffer,
                FloraSnapFlagBuffer = _floraSnapFlagBuffer,
                VisibleIndicesBuffer = visibleIndicesBuffer,
                GlobalFloatingOffset = globalFloatingOffset,
                PassMode = passMode,
                NearDistance = _nearLodDistance,
                FarDistance = _farLodDistance,
                TransitionRange = _lodTransitionRange,
                ImpostorWidth = _impostorWidth,
                ImpostorHeight = _impostorHeight,
                UseGpuIndirectFlag = ToBindingFlag(useGpuIndirect),
                IsValidFlag = BindingFlagTrue
            };
        }

        private bool MaterialBindingStateMatches(
            in MaterialBindingState state,
            Material material,
            GraphicsBuffer activeInstanceDataBuffer,
            GraphicsBuffer floraAgeBuffer,
            Vector4 globalFloatingOffset,
            float passMode,
            GraphicsBuffer visibleIndicesBuffer,
            bool useGpuIndirect)
        {
            return state.IsValidFlag != 0 &&
                ReferenceEquals(state.Material, material) &&
                ReferenceEquals(state.InstanceMatrixBuffer, _instanceMatrixBuffer) &&
                ReferenceEquals(state.InstanceDataBuffer, activeInstanceDataBuffer) &&
                ReferenceEquals(state.FloraAgeBuffer, floraAgeBuffer) &&
                ReferenceEquals(state.FloraPhaseSeedBuffer, _floraPhaseSeedBuffer) &&
                ReferenceEquals(state.FloraSnapFlagBuffer, _floraSnapFlagBuffer) &&
                ReferenceEquals(state.VisibleIndicesBuffer, visibleIndicesBuffer) &&
                state.GlobalFloatingOffset.x == globalFloatingOffset.x &&
                state.GlobalFloatingOffset.y == globalFloatingOffset.y &&
                state.GlobalFloatingOffset.z == globalFloatingOffset.z &&
                state.GlobalFloatingOffset.w == globalFloatingOffset.w &&
                state.PassMode == passMode &&
                state.NearDistance == _nearLodDistance &&
                state.FarDistance == _farLodDistance &&
                state.TransitionRange == _lodTransitionRange &&
                state.ImpostorWidth == _impostorWidth &&
                state.ImpostorHeight == _impostorHeight &&
                (state.UseGpuIndirectFlag != 0) == useGpuIndirect;
        }

        private void ApplyCullComputeBindings(
            ref ComputeCullBindingState state,
            int kernel,
            GraphicsBuffer activeInstanceDataBuffer,
            GraphicsBuffer floraAgeBuffer,
            bool shadowKernel)
        {
            if (_cullingCompute == null || kernel < 0 || _instanceMatrixBuffer == null || activeInstanceDataBuffer == null || floraAgeBuffer == null)
                return;

            GraphicsBuffer visibleLod0Buffer = shadowKernel ? null : _visibleIndicesLod0Buffer;
            GraphicsBuffer visibleLod1Buffer = shadowKernel ? null : _visibleIndicesLod1Buffer;
            GraphicsBuffer visibleShadowBuffer = shadowKernel ? _visibleIndicesShadowBuffer : null;
            GraphicsBuffer telemetryCountersBuffer = _cullTelemetryCountersBuffer;

            if (state.IsValidFlag != 0 &&
                ReferenceEquals(state.Shader, _cullingCompute) &&
                state.Kernel == kernel &&
                ReferenceEquals(state.MatrixBuffer, _instanceMatrixBuffer) &&
                ReferenceEquals(state.InstanceDataBuffer, activeInstanceDataBuffer) &&
                ReferenceEquals(state.FloraAgeBuffer, floraAgeBuffer) &&
                ReferenceEquals(state.VisibleLod0Buffer, visibleLod0Buffer) &&
                ReferenceEquals(state.VisibleLod1Buffer, visibleLod1Buffer) &&
                ReferenceEquals(state.VisibleShadowBuffer, visibleShadowBuffer) &&
                ReferenceEquals(state.TelemetryCountersBuffer, telemetryCountersBuffer) &&
                (state.IsShadowKernelFlag != 0) == shadowKernel)
            {
                return;
            }

            _cullingCompute.SetBuffer(kernel, _SourceMatricesId, _instanceMatrixBuffer);
            _cullingCompute.SetBuffer(kernel, _SourceDataId, activeInstanceDataBuffer);
            _cullingCompute.SetBuffer(kernel, _FloraAges01Id, floraAgeBuffer);
            if (shadowKernel)
            {
                if (visibleShadowBuffer != null)
                    _cullingCompute.SetBuffer(kernel, _VisibleIndicesShadowId, visibleShadowBuffer);
            }
            else
            {
                if (visibleLod0Buffer != null)
                    _cullingCompute.SetBuffer(kernel, _VisibleIndicesLod0Id, visibleLod0Buffer);
                if (visibleLod1Buffer != null)
                    _cullingCompute.SetBuffer(kernel, _VisibleIndicesLod1Id, visibleLod1Buffer);
            }

            if (telemetryCountersBuffer != null)
                _cullingCompute.SetBuffer(kernel, _CullTelemetryCountersId, telemetryCountersBuffer);

            state = new ComputeCullBindingState
            {
                Shader = _cullingCompute,
                Kernel = kernel,
                MatrixBuffer = _instanceMatrixBuffer,
                InstanceDataBuffer = activeInstanceDataBuffer,
                FloraAgeBuffer = floraAgeBuffer,
                VisibleLod0Buffer = visibleLod0Buffer,
                VisibleLod1Buffer = visibleLod1Buffer,
                VisibleShadowBuffer = visibleShadowBuffer,
                TelemetryCountersBuffer = telemetryCountersBuffer,
                IsShadowKernelFlag = ToBindingFlag(shadowKernel),
                IsValidFlag = BindingFlagTrue
            };
        }

        private void ApplySnapComputeBindings(
            ref ComputeSnapBindingState state,
            int kernel,
            GraphicsBuffer activeInstanceDataBuffer,
            bool clearKernel)
        {
            if (_abyssalFlowFieldCompute == null || kernel < 0 || _floraSnapFlagBuffer == null)
                return;

            GraphicsBuffer matrixBuffer = clearKernel ? null : _instanceMatrixBuffer;
            GraphicsBuffer instanceDataBuffer = clearKernel ? null : activeInstanceDataBuffer;
            if (!clearKernel && (matrixBuffer == null || instanceDataBuffer == null))
                return;

            if (state.IsValidFlag != 0 &&
                ReferenceEquals(state.Shader, _abyssalFlowFieldCompute) &&
                state.Kernel == kernel &&
                ReferenceEquals(state.MatrixBuffer, matrixBuffer) &&
                ReferenceEquals(state.InstanceDataBuffer, instanceDataBuffer) &&
                ReferenceEquals(state.SnapFlagBuffer, _floraSnapFlagBuffer) &&
                (state.IsClearKernelFlag != 0) == clearKernel)
            {
                return;
            }

            if (!clearKernel)
            {
                _abyssalFlowFieldCompute.SetBuffer(kernel, _SourceMatricesId, matrixBuffer);
                _abyssalFlowFieldCompute.SetBuffer(kernel, _SourceDataId, instanceDataBuffer);
            }

            _abyssalFlowFieldCompute.SetBuffer(kernel, _FloraSnapFlagsId, _floraSnapFlagBuffer);
            state = new ComputeSnapBindingState
            {
                Shader = _abyssalFlowFieldCompute,
                Kernel = kernel,
                MatrixBuffer = matrixBuffer,
                InstanceDataBuffer = instanceDataBuffer,
                SnapFlagBuffer = _floraSnapFlagBuffer,
                IsClearKernelFlag = ToBindingFlag(clearKernel),
                IsValidFlag = BindingFlagTrue
            };
        }

        private bool TryRenderGpuIndirect(
            Camera cullCamera,
            Mesh nearMesh,
            Mesh farMesh,
            Vector3 cameraPosition,
            Vector3 cameraForward,
            Bounds drawBounds)
        {
            if (!_preferGpuIndirectRendering ||
                !SystemInfo.supportsComputeShaders ||
                cullCamera == null ||
                nearMesh == null ||
                _cullingCompute == null ||
                _clearIndirectArgsKernel < 0 ||
                _instanceMatrixBuffer == null ||
                _instanceCount <= 0)
            {
                return false;
            }

            GraphicsBuffer activeInstanceDataBuffer = ResolveActiveInstanceDataBuffer();
            if (activeInstanceDataBuffer == null)
                return false;

            if (_frustumPlaneCache == null || _frustumPlaneCache.Length != FrustumPlaneCount)
                return false;

            GeometryUtility.CalculateFrustumPlanes(cullCamera, _frustumPlaneCache);
            if (!GeometryUtility.TestPlanesAABB(_frustumPlaneCache, drawBounds))
                return true;
            PopulateFrustumPlaneUpload();

            if (_instanceCount <= 0)
                return true;

            EnsureGpuIndirectResources(_instanceCount, nearMesh, farMesh);
            if (_visibleIndicesLod0Buffer == null || _indirectArgsLod0Buffer == null)
                return false;

            if (!TryBindGpuIndirectMaterials(activeInstanceDataBuffer, farMesh))
                return false;

            UpdateMotionVectorHistory(cullCamera, cameraPosition);
            bool depthPyramidReady = BuildDepthPyramid(cullCamera);
            DispatchGpuCulling(cullCamera, activeInstanceDataBuffer, depthPyramidReady, cameraPosition, cameraForward);

            RenderIndirectPass(_nearBrgMaterial, _nearIndirectProperties, nearMesh, _indirectArgsLod0Buffer, drawBounds, ShadowCastingMode.Off, _receiveShadows, MotionVectorGenerationMode.Camera, cullCamera);
            if (farMesh != null && _farBrgMaterial != null && _indirectArgsLod1Buffer != null)
                RenderIndirectPass(_farBrgMaterial, _farIndirectProperties, farMesh, _indirectArgsLod1Buffer, drawBounds, ShadowCastingMode.Off, _impostorReceiveShadows, MotionVectorGenerationMode.Camera, cullCamera);

            if (_enableDepthPrepass)
            {
                RenderIndirectPass(_depthNearBrgMaterial, _depthNearIndirectProperties, nearMesh, _indirectArgsLod0Buffer, drawBounds, ShadowCastingMode.Off, false, MotionVectorGenerationMode.Camera, cullCamera);
                if (farMesh != null && _depthFarBrgMaterial != null && _indirectArgsLod1Buffer != null)
                    RenderIndirectPass(_depthFarBrgMaterial, _depthFarIndirectProperties, farMesh, _indirectArgsLod1Buffer, drawBounds, ShadowCastingMode.Off, false, MotionVectorGenerationMode.Camera, cullCamera);
            }

            if (_enableShadowCasterDraw && _shadowBrgMaterial != null && _indirectArgsShadowBuffer != null && HasMainDirectionalShadowLight())
                RenderIndirectPass(_shadowBrgMaterial, _shadowIndirectProperties, nearMesh, _indirectArgsShadowBuffer, drawBounds, ShadowCastingMode.On, false, MotionVectorGenerationMode.Camera, cullCamera);

            if (_enableMotionVectorDraw)
            {
                RenderIndirectPass(_motionNearBrgMaterial, _motionNearIndirectProperties, nearMesh, _indirectArgsLod0Buffer, drawBounds, ShadowCastingMode.Off, false, MotionVectorGenerationMode.Object, cullCamera);
                if (farMesh != null && _motionFarBrgMaterial != null && _indirectArgsLod1Buffer != null)
                    RenderIndirectPass(_motionFarBrgMaterial, _motionFarIndirectProperties, farMesh, _indirectArgsLod1Buffer, drawBounds, ShadowCastingMode.Off, false, MotionVectorGenerationMode.Object, cullCamera);
            }

            return true;
        }

        private void RenderIndirectPass(
            Material material,
            MaterialPropertyBlock propertyBlock,
            Mesh mesh,
            GraphicsBuffer argsBuffer,
            Bounds drawBounds,
            ShadowCastingMode shadowCastingMode,
            bool receiveShadows,
            MotionVectorGenerationMode motionVectorMode,
            Camera cullCamera)
        {
            if (material == null || propertyBlock == null || mesh == null || argsBuffer == null)
                return;

            RenderParams renderParams = new RenderParams(material)
            {
                matProps = propertyBlock,
                worldBounds = drawBounds,
                layer = gameObject.layer,
                shadowCastingMode = shadowCastingMode,
                receiveShadows = receiveShadows,
                motionVectorMode = motionVectorMode,
                camera = _cameraOverride != null ? _cameraOverride : cullCamera
            };
            UnityEngine.Graphics.RenderMeshIndirect(renderParams, mesh, argsBuffer, 1, 0);
        }

        private void DispatchGpuCulling(
            Camera cullCamera,
            GraphicsBuffer activeInstanceDataBuffer,
            bool depthPyramidReady,
            Vector3 cameraPosition,
            Vector3 cameraForward)
        {
            if (_cullingCompute == null ||
                _cullFloraKernel < 0 ||
                _visibleIndicesLod0Buffer == null ||
                _indirectArgsLod0Buffer == null ||
                _instanceCount <= 0)
            {
                return;
            }

            Vector4 globalFloatingOffset = ResolveVegetationFloatingOffset();
            Matrix4x4 viewProjection = GL.GetGPUProjectionMatrix(cullCamera.projectionMatrix, false) * cullCamera.worldToCameraMatrix;
            Matrix4x4 viewMatrix = cullCamera.worldToCameraMatrix;

            Mesh nearMesh = ResolveNearRenderMesh();
            Mesh farMesh = FrameTimeWatchdog.IsDistantFloraRenderingEnabled && _farLodDistance > _nearLodDistance
                ? ResolveImpostorRenderMesh()
                : null;
            float brgLodDistanceScalar = VRAMPressureMonitor.BrgLodDistanceScalar;
            float brgNearLodDistance = Mathf.Max(0.01f, _nearLodDistance * brgLodDistanceScalar);
            float brgFarLodDistance = Mathf.Max(brgNearLodDistance, _farLodDistance * brgLodDistanceScalar);
            float brgLodTransitionRange = Mathf.Max(0.01f, _lodTransitionRange * brgLodDistanceScalar);
            int densityDecimationStep = ResolveDensityDecimationStep();
            _resolvedDensityDecimationStep = densityDecimationStep;
            EnsureCullTelemetryCounterBuffer();
            bool sampleCullTelemetry = BeginCullTelemetrySample();
            bool hasFarLod = farMesh != null && _visibleIndicesLod1Buffer != null && _indirectArgsLod1Buffer != null;
            bool farCadenceEligible = hasFarLod &&
                                      _farCullingFrameStride > 1 &&
                                      brgFarLodDistance > _farCullingCadenceDistance;
            bool updateFarLodThisFrame = hasFarLod &&
                                         (!_hasFarCullingSnapshot ||
                                          !farCadenceEligible ||
                                          (_gpuCullingFrameIndex % _farCullingFrameStride) == 0);
            _gpuCullingFrameIndex = (_gpuCullingFrameIndex + 1) & 0x3fffffff;
            if (!hasFarLod)
                _hasFarCullingSnapshot = false;

            _visibleIndicesLod0Buffer.SetCounterValue(0u);
            if (updateFarLodThisFrame)
                _visibleIndicesLod1Buffer.SetCounterValue(0u);
            _visibleIndicesShadowBuffer?.SetCounterValue(0u);

            if (!ClearIndirectArgsBuffer(_indirectArgsLod0Buffer, nearMesh) ||
                (hasFarLod && updateFarLodThisFrame && !ClearIndirectArgsBuffer(_indirectArgsLod1Buffer, farMesh)) ||
                (!hasFarLod && _indirectArgsLod1Buffer != null && !ClearIndirectArgsBuffer(_indirectArgsLod1Buffer, nearMesh)) ||
                !ClearIndirectArgsBuffer(_indirectArgsShadowBuffer, nearMesh))
            {
                return;
            }

            GraphicsBuffer floraAgeBuffer = ResolveFloraAgeBuffer();
            if (floraAgeBuffer == null)
                return;

            ApplyCullComputeBindings(
                ref _mainCullComputeBindingState,
                _cullFloraKernel,
                activeInstanceDataBuffer,
                floraAgeBuffer,
                shadowKernel: false);
            _cullingCompute.SetInt(_FarLodAppendEnabledId, updateFarLodThisFrame ? 1 : 0);
            _cullingCompute.SetInt(_DensityDecimationStepId, densityDecimationStep);
            _cullingCompute.SetInt(_CullTelemetryEnabledId, sampleCullTelemetry ? 1 : 0);
            _cullingCompute.SetInt(_SourceInstanceCountId, _instanceCount);
            _cullingCompute.SetMatrix(_ViewProjectionId, viewProjection);
            _cullingCompute.SetMatrix(_ViewMatrixId, viewMatrix);
            _cullingCompute.SetVector(_CameraPositionId, cameraPosition);
            _cullingCompute.SetVector(_CameraForwardId, cameraForward);
            _cullingCompute.SetVector(_GlobalFloatingOffsetId, globalFloatingOffset);
            _cullingCompute.SetFloat(_LodNearDistanceId, brgNearLodDistance);
            _cullingCompute.SetFloat(_LodFarDistanceId, brgFarLodDistance);
            _cullingCompute.SetFloat(_LodTransitionRangeId, brgLodTransitionRange);
            float peripheralCullDot = Mathf.Clamp(_peripheralCullDot, -1f, 1f);
            float peripheralCullDistance = Mathf.Max(0f, _peripheralCullDistance);
            float peripheralCullDistanceSq = peripheralCullDistance * peripheralCullDistance;
            _cullingCompute.SetFloat(_PeripheralCullDotId, peripheralCullDot);
            _cullingCompute.SetFloat(_PeripheralCullDistanceSqId, peripheralCullDistanceSq);
            _cullingCompute.SetFloat(_OcclusionDepthBiasId, _occlusionDepthBias);
            _cullingCompute.SetInt(_OcclusionEnabledId, depthPyramidReady && _enableDepthOcclusion ? 1 : 0);
            _cullingCompute.SetVector(_OcclusionZBufferParamsId, Shader.GetGlobalVector(_GlobalZBufferParamsId));
            _cullingCompute.SetInt(_DarknessCullEnabledId, _enableDarknessCulling ? 1 : 0);
            _cullingCompute.SetFloat(_DarknessBiolumThresholdId, _darknessBiolumThreshold);
            _cullingCompute.SetVectorArray(_FrustumPlanesId, _frustumPlaneVectors);
            if (_depthPyramidTexture != null)
                _cullingCompute.SetTexture(_cullFloraKernel, _DepthPyramidTextureId, _depthPyramidTexture);
            _cullingCompute.SetInt(_DepthPyramidMipCountId, _depthPyramidMipCount);
            _cullingCompute.SetVector(_DepthPyramidTexelSizeId, new Vector4(
                _depthPyramidWidth > 0 ? 1f / _depthPyramidWidth : 0f,
                _depthPyramidHeight > 0 ? 1f / _depthPyramidHeight : 0f,
                _depthPyramidWidth,
                _depthPyramidHeight));

            int headlightCount = CopyScooterHeadlightPayload();
            ApplyScooterHeadlightPayloadToCullCompute(headlightCount, uploadPayloadArrays: true);
            _cullingCompute.SetFloat(_FloorBiolumStrengthId, Shader.GetGlobalFloat(_FloorBiolumStrengthId));
            _cullingCompute.SetFloat(_OceanBiolumStrengthId, Shader.GetGlobalFloat(_OceanBiolumStrengthId));
            _cullingCompute.SetFloat(_BiolumIntensityVectorId, ResolveBiolumIntensityScalar());

            int dispatchGroups = Mathf.Max(1, (_instanceCount + ThreadsPerGroup - 1) / ThreadsPerGroup);
            DispatchFloraSnapFlagUpdate(activeInstanceDataBuffer, globalFloatingOffset, dispatchGroups);
            _cullingCompute.Dispatch(_cullFloraKernel, dispatchGroups, 1, 1);

            if (_visibleIndicesShadowBuffer != null && _cullFloraShadowKernel >= 0)
            {
                ApplyCullComputeBindings(
                    ref _shadowCullComputeBindingState,
                    _cullFloraShadowKernel,
                    activeInstanceDataBuffer,
                    floraAgeBuffer,
                    shadowKernel: true);
                _cullingCompute.SetInt(_DensityDecimationStepId, densityDecimationStep);
                _cullingCompute.SetInt(_CullTelemetryEnabledId, 0);
                _cullingCompute.SetInt(_SourceInstanceCountId, _instanceCount);
                _cullingCompute.SetMatrix(_ViewProjectionId, viewProjection);
                _cullingCompute.SetMatrix(_ViewMatrixId, viewMatrix);
                _cullingCompute.SetVector(_CameraPositionId, cameraPosition);
                _cullingCompute.SetVector(_CameraForwardId, cameraForward);
                _cullingCompute.SetVector(_GlobalFloatingOffsetId, globalFloatingOffset);
                _cullingCompute.SetFloat(_LodNearDistanceId, brgNearLodDistance);
                _cullingCompute.SetFloat(_LodFarDistanceId, brgFarLodDistance);
                _cullingCompute.SetFloat(_LodTransitionRangeId, brgLodTransitionRange);
                _cullingCompute.SetFloat(_PeripheralCullDotId, peripheralCullDot);
                _cullingCompute.SetFloat(_PeripheralCullDistanceSqId, peripheralCullDistanceSq);
                _cullingCompute.SetInt(_DarknessCullEnabledId, _enableDarknessCulling ? 1 : 0);
                _cullingCompute.SetFloat(_DarknessBiolumThresholdId, _darknessBiolumThreshold);
                ApplyScooterHeadlightPayloadToCullCompute(headlightCount, uploadPayloadArrays: false);
                _cullingCompute.SetVectorArray(_FrustumPlanesId, _frustumPlaneVectors);
                _cullingCompute.Dispatch(_cullFloraShadowKernel, dispatchGroups, 1, 1);
            }

            GraphicsBuffer.CopyCount(_visibleIndicesLod0Buffer, _indirectArgsLod0Buffer, sizeof(uint));
            if (updateFarLodThisFrame && _visibleIndicesLod1Buffer != null && _indirectArgsLod1Buffer != null)
            {
                GraphicsBuffer.CopyCount(_visibleIndicesLod1Buffer, _indirectArgsLod1Buffer, sizeof(uint));
                _hasFarCullingSnapshot = true;
            }
            if (_visibleIndicesShadowBuffer != null && _indirectArgsShadowBuffer != null)
                GraphicsBuffer.CopyCount(_visibleIndicesShadowBuffer, _indirectArgsShadowBuffer, sizeof(uint));

            RequestCullTelemetryReadback(sampleCullTelemetry);
        }

        private void DispatchFloraSnapFlagUpdate(GraphicsBuffer activeInstanceDataBuffer, Vector4 globalFloatingOffset, int dispatchGroups)
        {
            if (_abyssalFlowFieldCompute == null ||
                _flagSnappedFloraKernel < 0 ||
                _floraSnapFlagBuffer == null ||
                _instanceMatrixBuffer == null ||
                activeInstanceDataBuffer == null ||
                _instanceCount <= 0)
            {
                return;
            }

            if (_floraSnapFlagBufferRequiresClear && _clearFloraSnapFlagsKernel >= 0)
            {
                ApplySnapComputeBindings(
                    ref _clearSnapComputeBindingState,
                    _clearFloraSnapFlagsKernel,
                    activeInstanceDataBuffer,
                    clearKernel: true);
                _abyssalFlowFieldCompute.SetInt(_SourceInstanceCountId, _instanceCount);
                _abyssalFlowFieldCompute.Dispatch(_clearFloraSnapFlagsKernel, dispatchGroups, 1, 1);
                _floraSnapFlagBufferRequiresClear = false;
            }

            Vector4 washVelocity = Shader.GetGlobalVector(_SubmarineWashVelocityId);
            Vector4 washSphere = Shader.GetGlobalVector(_SubmarineWashSphereId);
            if (washVelocity.w <= 10f || washSphere.w <= 0f)
                return;

            ApplySnapComputeBindings(
                ref _flagSnapComputeBindingState,
                _flagSnappedFloraKernel,
                activeInstanceDataBuffer,
                clearKernel: false);
            _abyssalFlowFieldCompute.SetInt(_SourceInstanceCountId, _instanceCount);
            _abyssalFlowFieldCompute.SetVector(_GlobalFloatingOffsetId, globalFloatingOffset);
            _abyssalFlowFieldCompute.SetVector(_SubmarineWashSphereId, washSphere);
            _abyssalFlowFieldCompute.SetVector(_SubmarineWashVelocityId, washVelocity);
            _abyssalFlowFieldCompute.Dispatch(_flagSnappedFloraKernel, dispatchGroups, 1, 1);
        }

        private void ApplyScooterHeadlightPayloadToCullCompute(int headlightCount, bool uploadPayloadArrays)
        {
            _cullingCompute.SetInt(_ScooterHeadlightCountId, headlightCount);

            if (!uploadPayloadArrays || headlightCount <= 0)
                return;

            _cullingCompute.SetVectorArray(_ScooterHeadlightPositionsWsId, _scooterHeadlightPositionsWs);
            _cullingCompute.SetVectorArray(_ScooterHeadlightDirectionsWsId, _scooterHeadlightDirectionsWs);
            _cullingCompute.SetVectorArray(_ScooterHeadlightColorsId, _scooterHeadlightColors);
            _cullingCompute.SetVectorArray(_ScooterHeadlightConeDataId, _scooterHeadlightConeData);
        }

        private void EnsureAndDispatchFloraSnapFlags(GraphicsBuffer activeInstanceDataBuffer, Vector4 globalFloatingOffset)
        {
            if (!SystemInfo.supportsComputeShaders ||
                _abyssalFlowFieldCompute == null ||
                _clearFloraSnapFlagsKernel < 0 ||
                _flagSnappedFloraKernel < 0 ||
                _instanceMatrixBuffer == null ||
                activeInstanceDataBuffer == null ||
                _instanceCount <= 0)
            {
                ReleaseFloraSnapFlagBuffer();
                return;
            }

            EnsureFloraSnapFlagBufferCapacity(Mathf.NextPowerOfTwo(Mathf.Max(1, _instanceCount)));
            if (_floraSnapFlagBuffer == null)
                return;

            int dispatchGroups = Mathf.Max(1, (_instanceCount + ThreadsPerGroup - 1) / ThreadsPerGroup);
            DispatchFloraSnapFlagUpdate(activeInstanceDataBuffer, globalFloatingOffset, dispatchGroups);
        }

        private bool BuildDepthPyramid(Camera cullCamera)
        {
            if (!_enableDepthOcclusion || _depthPyramidCompute == null || cullCamera == null)
                return false;

            Texture depthTexture = Shader.GetGlobalTexture(_GlobalCameraDepthTextureId);
            if (depthTexture == null)
                return false;

            int targetWidth = Mathf.Max(1, cullCamera.pixelWidth);
            int targetHeight = Mathf.Max(1, cullCamera.pixelHeight);
            EnsureDepthPyramidResources(targetWidth, targetHeight);
            if (_depthPyramidTexture == null || _depthPyramidCopyKernel < 0 || _depthPyramidDownsampleKernel < 0)
                return false;

            _depthPyramidCompute.SetTexture(_depthPyramidCopyKernel, _DepthPyramidSourceDepthId, depthTexture);
            _depthPyramidCompute.SetTexture(_depthPyramidCopyKernel, _DepthPyramidTargetId, _depthPyramidTexture, 0);
            _depthPyramidCompute.Dispatch(
                _depthPyramidCopyKernel,
                Mathf.Max(1, (_depthPyramidWidth + 7) / 8),
                Mathf.Max(1, (_depthPyramidHeight + 7) / 8),
                1);

            for (int mipIndex = 1; mipIndex < _depthPyramidMipCount; mipIndex++)
            {
                int mipWidth = Mathf.Max(1, _depthPyramidWidth >> mipIndex);
                int mipHeight = Mathf.Max(1, _depthPyramidHeight >> mipIndex);
                _depthPyramidCompute.SetTexture(_depthPyramidDownsampleKernel, _DepthPyramidSourceId, _depthPyramidTexture, mipIndex - 1);
                _depthPyramidCompute.SetTexture(_depthPyramidDownsampleKernel, _DepthPyramidTargetId, _depthPyramidTexture, mipIndex);
                _depthPyramidCompute.Dispatch(
                    _depthPyramidDownsampleKernel,
                    Mathf.Max(1, (mipWidth + 7) / 8),
                    Mathf.Max(1, (mipHeight + 7) / 8),
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
                name = "__HectonVegetationDepthPyramid",
                hideFlags = HideFlags.HideAndDontSave,
                enableRandomWrite = true,
                useMipMap = true,
                autoGenerateMips = false,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            }; // COLD ALLOC: RenderTexture[targetWidth x targetHeight] - vegetation Hi-Z depth pyramid for compute occlusion - owner: HectonIndirectVegetationRenderer
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

        private static float ResolveBiolumIntensityScalar()
        {
            Vector4 intensity = Shader.GetGlobalVector(_BiolumIntensityVectorId);
            float scalar = intensity.x;
            return math.isfinite(scalar) ? math.max(0f, scalar) : 0f;
        }

        private void EnsureGpuIndirectResources(int instanceCount, Mesh nearMesh, Mesh farMesh)
        {
            int requiredCapacity = Mathf.NextPowerOfTwo(Mathf.Max(1, instanceCount));
            if (requiredCapacity != _gpuVisibleIndexCapacity)
            {
                ReleaseVisibleIndexBuffer(ref _visibleIndicesLod0Buffer);
                ReleaseVisibleIndexBuffer(ref _visibleIndicesLod1Buffer);
                ReleaseVisibleIndexBuffer(ref _visibleIndicesShadowBuffer);
                _visibleIndicesLod0Buffer = new GraphicsBuffer(GraphicsBuffer.Target.Append, requiredCapacity, VisibleIndexStride); // COLD ALLOC: GraphicsBuffer[visibleCapacity] - near vegetation visible-instance append buffer - owner: HectonIndirectVegetationRenderer
                _visibleIndicesLod1Buffer = new GraphicsBuffer(GraphicsBuffer.Target.Append, requiredCapacity, VisibleIndexStride); // COLD ALLOC: GraphicsBuffer[visibleCapacity] - far vegetation visible-instance append buffer - owner: HectonIndirectVegetationRenderer
                _visibleIndicesShadowBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Append, requiredCapacity, VisibleIndexStride); // COLD ALLOC: GraphicsBuffer[visibleCapacity] - shadow vegetation visible-instance append buffer - owner: HectonIndirectVegetationRenderer
                _gpuVisibleIndexCapacity = requiredCapacity;
                _hasFarCullingSnapshot = false;
                ResetCullComputeBindingStates();
            }

            EnsureIndirectArgsBuffer(ref _indirectArgsLod0Buffer);
            EnsureIndirectArgsBuffer(ref _indirectArgsLod1Buffer);
            EnsureIndirectArgsBuffer(ref _indirectArgsShadowBuffer);
            EnsureCullTelemetryCounterBuffer();
            if (_abyssalFlowFieldCompute != null && _clearFloraSnapFlagsKernel >= 0 && _flagSnappedFloraKernel >= 0)
                EnsureFloraSnapFlagBufferCapacity(requiredCapacity);
            else
                ReleaseFloraSnapFlagBuffer();
        }

        private void EnsureIndirectArgsBuffer(ref GraphicsBuffer argsBuffer)
        {
            if (argsBuffer == null)
                argsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Raw, 1, GraphicsBuffer.IndirectDrawIndexedArgs.size); // COLD ALLOC: GraphicsBuffer[1] - GPU-cleared indirect indexed draw arguments for vegetation pass - owner: HectonIndirectVegetationRenderer
        }

        private void EnsureCullTelemetryCounterBuffer()
        {
            if (!_enableCullTelemetry &&
                _cullTelemetryCountersBuffer != null &&
                _cullTelemetryCountersBuffer.IsValid())
            {
                // The compute kernel declares this RW buffer. Keep a tiny bound dummy even when sampling is disabled.
                return;
            }

            if (_cullTelemetryCountersBuffer != null &&
                _cullTelemetryCountersBuffer.IsValid() &&
                _cullTelemetryCountersBuffer.count >= ScatterCullTelemetryCounterCount)
            {
                return;
            }

            ReleaseGraphicsBuffer(ref _cullTelemetryCountersBuffer);
            _cullTelemetryCountersBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                ScatterCullTelemetryCounterCount,
                sizeof(uint)); // COLD ALLOC: GraphicsBuffer[4] - GPU cull telemetry counters for SHINOBU_09 scatter diagnostics - owner: HectonIndirectVegetationRenderer
            ResetCullComputeBindingStates();
        }

        private void EnsureFloraSnapFlagBufferCapacity(int requiredCapacity)
        {
            if (requiredCapacity <= 0)
                return;

            if (_floraSnapFlagBuffer != null &&
                _floraSnapFlagBuffer.IsValid() &&
                _floraSnapFlagCapacity >= requiredCapacity)
            {
                return;
            }

            ReleaseGraphicsBuffer(ref _floraSnapFlagBuffer);
            _floraSnapFlagBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, requiredCapacity, sizeof(uint)); // COLD ALLOC: GraphicsBuffer[visibleCapacity] - persistent GPU-only snapped flora flags - owner: HectonIndirectVegetationRenderer
            _floraSnapFlagCapacity = requiredCapacity;
            _floraSnapFlagBufferRequiresClear = true;
            ResetSnapComputeBindingStates();
        }

        private void ReleaseFloraSnapFlagBuffer()
        {
            ReleaseGraphicsBuffer(ref _floraSnapFlagBuffer);
            _floraSnapFlagCapacity = 0;
            _floraSnapFlagBufferRequiresClear = false;
            ResetSnapComputeBindingStates();
        }

        private GraphicsBuffer ResolveFloraAgeBuffer()
        {
            if (_instanceCount <= 0)
                return null;

            EnsureFloraAgeCapacity(_instanceCount);
            if (_floraAgeBuffer == null || !_floraAges01.IsCreated)
                return null;

            if (_floraAgeBufferDirty)
            {
                if (!_hasCpuCullingData && !_floraAgesAuthoredExternally)
                    FillDefaultFloraAges(_instanceCount);

                RecordFloraGrowthTelemetry(_instanceCount, true);
                if (_floraAgesAuthoredExternally)
                    SanitizeFloraAgeBufferForUpload(_instanceCount);
                GraphicsBufferUploadUtility.UploadNativeArray(_floraAgeBuffer, _floraAges01, _instanceCount);
                _floraAgeBufferDirty = false;
            }
            else
            {
                RecordFloraGrowthTelemetry(_instanceCount, false);
            }

            return _floraAgeBuffer;
        }

        private void EnsureFloraAgeCapacity(int requiredCount)
        {
            if (requiredCount <= 0)
                return;

            int requiredCapacity = Mathf.NextPowerOfTwo(Mathf.Max(16, requiredCount));
            if (_floraAgeBuffer != null &&
                _floraAgeBuffer.IsValid() &&
                _floraAgeCapacity >= requiredCapacity &&
                _floraAges01.IsCreated &&
                _floraAges01.Length >= requiredCapacity)
            {
                return;
            }

            ReleaseFloraAgeResources();
            _floraAges01 = new NativeArray<float>(requiredCapacity, DataVaultExemptVegetationAgeLaneAllocator, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<float>[NextPowerOfTwo(instanceCount)] - flora age SoA upload lane - owner: HectonIndirectVegetationRenderer
            NativeMemorySentinel.RegisterNativeArray(_floraAges01, nameof(HectonIndirectVegetationRenderer), nameof(_floraAges01), NativeAllocationLifetime.Session);
            _floraAgeBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float>(requiredCapacity); // COLD ALLOC: GraphicsBuffer[NextPowerOfTwo(instanceCount)] - StructuredBuffer<float> flora age lane - owner: HectonIndirectVegetationRenderer
            _floraAgeCapacity = requiredCapacity;
            FillDefaultFloraAges(requiredCapacity);
            _floraAgeBufferDirty = true;
        }

        private void FillDefaultFloraAges(int count)
        {
            if (!_floraAges01.IsCreated)
                return;

            int safeCount = Mathf.Min(count, _floraAges01.Length);
            for (int instanceIndex = 0; instanceIndex < safeCount; instanceIndex++)
                _floraAges01[instanceIndex] = 1f;
        }

        private void SanitizeFloraAgeBufferForUpload(int count)
        {
            if (!_floraAges01.IsCreated)
                return;

            int safeCount = math.min(count, _floraAges01.Length);
            for (int instanceIndex = 0; instanceIndex < safeCount; instanceIndex++)
                _floraAges01[instanceIndex] = SanitizeFloraAgeForUpload(_floraAges01[instanceIndex]);
        }

        private void ReleaseFloraAgeResources()
        {
            ReleaseGraphicsBuffer(ref _floraAgeBuffer);
            if (_floraAges01.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_floraAges01);
                _floraAges01.Dispose();
            }

            _floraAgeCapacity = 0;
            _floraAgesAuthoredExternally = false;
            _floraAgeBufferDirty = true;
        }

        private static float SanitizeFloraAgeForUpload(float age01)
        {
            if (!math.isfinite(age01))
                return -1f;

            if (age01 < 0f)
                return -1f;

            return math.saturate(age01);
        }

        private static float ResolveFloraAgeFromMetadata(in HectonVegetationInstanceData metadata)
        {
            if (metadata.Reserved0 < 0f)
                return -1f;

            byte runtimeFlags = HectonVegetationRuntimeFlagEncoding.ExtractPackedFlags(metadata.RuntimeFlags);
            if ((runtimeFlags & (byte)HectonVegetationRuntimeFlags.Dead) != 0)
                return -1f;

            if (metadata.Reserved0 > 0.0001f)
                return Mathf.Clamp01(metadata.Reserved0);

            return 1f;
        }

        private void EnsureFloraGrowthTelemetry()
        {
            if (_floraGrowthTelemetry.IsCreated)
                return;

            _floraGrowthTelemetry = new NativeArray<FloraGrowthTelemetryEntry>(
                FloraGrowthTelemetryFrameCount,
                DataVaultExemptVegetationTelemetryAllocator,
                NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<FloraGrowthTelemetryEntry>[300] - flora growth black-box circular telemetry - owner: HectonIndirectVegetationRenderer
            NativeMemorySentinel.RegisterNativeArray(_floraGrowthTelemetry, nameof(HectonIndirectVegetationRenderer), nameof(_floraGrowthTelemetry), NativeAllocationLifetime.Session);
        }

        private void ReleaseFloraGrowthTelemetryResources()
        {
            if (!_floraGrowthTelemetry.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(_floraGrowthTelemetry);
            _floraGrowthTelemetry.Dispose();
            _floraGrowthTelemetryCursor = 0;
            _lastFloraGrowthTelemetryFrame = -1;
        }

        private void RecordFloraGrowthTelemetry(int instanceCount, bool fullScan)
        {
            if (instanceCount <= 0 || !_floraAges01.IsCreated)
                return;

            int frameIndex = Time.frameCount;
            if (_lastFloraGrowthTelemetryFrame == frameIndex)
                return;

            _lastFloraGrowthTelemetryFrame = frameIndex;
            EnsureFloraGrowthTelemetry();
            if (!_floraGrowthTelemetry.IsCreated)
                return;

            int safeCount = math.min(instanceCount, _floraAges01.Length);
            int sampleLimit = fullScan ? safeCount : math.min(safeCount, FloraGrowthTelemetryMaxSamples);
            int stride = sampleLimit > 0 ? math.max(1, (safeCount + sampleLimit - 1) / sampleLimit) : 1;
            int sampled = 0;
            int negativeCount = 0;
            int nanCount = 0;
            uint ageHash = FloraGrowthTelemetryHashSeed;
            float minAge = 1f;
            float maxAge = 0f;

            for (int instanceIndex = 0; instanceIndex < safeCount; instanceIndex += stride)
            {
                float age = _floraAges01[instanceIndex];
                if (!math.isfinite(age))
                {
                    nanCount++;
                    age = -1f;
                }

                if (age < 0f)
                {
                    negativeCount++;
                }
                else
                {
                    minAge = math.min(minAge, age);
                    maxAge = math.max(maxAge, age);
                }

                ageHash = HashFloraGrowthSample(ageHash, instanceIndex, age);
                sampled++;
                if (!fullScan && sampled >= FloraGrowthTelemetryMaxSamples)
                    break;
            }

            if (sampled == 0)
            {
                minAge = 0f;
                maxAge = 0f;
            }

            int writeIndex = _floraGrowthTelemetryCursor;
            _floraGrowthTelemetry[writeIndex] = new FloraGrowthTelemetryEntry
            {
                FrameIndex = frameIndex,
                InstanceCount = safeCount,
                SampleCount = sampled,
                NegativeAgeCount = negativeCount,
                NanAgeCount = nanCount,
                DirtyUpload = fullScan ? 1 : 0,
                MinAge01 = minAge,
                MaxAge01 = maxAge,
                AgeHash = ageHash
            };
            _floraGrowthTelemetryCursor = (_floraGrowthTelemetryCursor + 1) % FloraGrowthTelemetryFrameCount;

            if (nanCount > 0 && !_floraGrowthTelemetryDumped)
                DumpFloraGrowthTelemetry();
        }

        private static uint HashFloraGrowthSample(uint hash, int instanceIndex, float age01)
        {
            unchecked
            {
                hash ^= (uint)instanceIndex + 0x9E3779B9u + (hash << 6) + (hash >> 2);
                hash ^= math.asuint(age01);
                hash *= 16777619u;
                return hash;
            }
        }

        private void DumpFloraGrowthTelemetry()
        {
            _floraGrowthTelemetryDumped = true;
            if (!_floraGrowthTelemetry.IsCreated)
                return;

            try
            {
                string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                string dumpPath = Path.Combine(projectRoot, FloraGrowthDumpRelativePath);
                string dumpDirectory = Path.GetDirectoryName(dumpPath);
                if (!string.IsNullOrEmpty(dumpDirectory))
                    Directory.CreateDirectory(dumpDirectory);

                using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(FloraGrowthTelemetryDumpVersion);
                    writer.Write(FloraGrowthTelemetryFrameCount);
                    writer.Write(_floraGrowthTelemetryCursor);
                    writer.Write(_instanceCount);

                    for (int offset = 0; offset < FloraGrowthTelemetryFrameCount; offset++)
                    {
                        int readIndex = (_floraGrowthTelemetryCursor + offset) % FloraGrowthTelemetryFrameCount;
                        FloraGrowthTelemetryEntry entry = _floraGrowthTelemetry[readIndex];
                        writer.Write(entry.FrameIndex);
                        writer.Write(entry.InstanceCount);
                        writer.Write(entry.SampleCount);
                        writer.Write(entry.NegativeAgeCount);
                        writer.Write(entry.NanAgeCount);
                        writer.Write(entry.DirtyUpload);
                        writer.Write(entry.MinAge01);
                        writer.Write(entry.MaxAge01);
                        writer.Write(entry.AgeHash);
                        writer.Write(entry.Reserved0);
                    }
                }
            }
            catch (Exception exception)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError($"[HectonIndirectVegetationRenderer] Failed to dump flora growth telemetry: {exception.Message}", this);
#endif
            }
        }

        private bool BeginCullTelemetrySample()
        {
            if (!_enableCullTelemetry ||
                _cullTelemetryCountersBuffer == null ||
                _cullTelemetryClearPayload == null ||
                _scatterCullTelemetryReadbackPending)
            {
                return false;
            }

            int frameIndex = Time.frameCount;
            if (frameIndex == _lastScatterCullTelemetrySampleFrame ||
                frameIndex % ScatterCullTelemetryReadbackStrideFrames != 0)
            {
                return false;
            }

            _lastScatterCullTelemetrySampleFrame = frameIndex;
            _cullTelemetryCountersBuffer.SetData(_cullTelemetryClearPayload, 0, 0, ScatterCullTelemetryCounterCount);
            return true;
        }

        private void RequestCullTelemetryReadback(bool sampleCullTelemetry)
        {
            if (!sampleCullTelemetry || _cullTelemetryCountersBuffer == null || _scatterCullTelemetryReadbackPending)
                return;

            _cullTelemetryReadbackRequest = AsyncGPUReadback.Request(_cullTelemetryCountersBuffer);
            _scatterCullTelemetryReadbackPending = true;
        }

        private void PollCullTelemetryReadback()
        {
            if (!_scatterCullTelemetryReadbackPending || !_cullTelemetryReadbackRequest.done)
                return;

            _scatterCullTelemetryReadbackPending = false;
            if (_cullTelemetryReadbackRequest.hasError)
                return;

            NativeArray<uint> counters = _cullTelemetryReadbackRequest.GetData<uint>();
            if (!counters.IsCreated || counters.Length < ScatterCullTelemetryCounterCount)
                return;

            int totalCount = ClampCounterToInt(counters[ScatterCullTelemetryTotalCounter]);
            int frustumCount = ClampCounterToInt(counters[ScatterCullTelemetryFrustumCounter]);
            int occlusionCount = ClampCounterToInt(counters[ScatterCullTelemetryOcclusionCounter]);
            int visibleCount = ClampCounterToInt(counters[ScatterCullTelemetryVisibleCounter]);
            RecordScatterCullTelemetry(totalCount, frustumCount, occlusionCount, visibleCount);
        }

        private void EnsureScatterCullTelemetry()
        {
            if (_scatterCullTelemetry.IsCreated)
                return;

            _scatterCullTelemetry = new NativeArray<ScatterCullTelemetryEntry>(
                ScatterCullTelemetryFrameCount,
                DataVaultExemptVegetationTelemetryAllocator,
                NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<ScatterCullTelemetryEntry>[300] - SHINOBU_09 cull black-box circular telemetry - owner: HectonIndirectVegetationRenderer
            NativeMemorySentinel.RegisterNativeArray(_scatterCullTelemetry, nameof(HectonIndirectVegetationRenderer), nameof(_scatterCullTelemetry), NativeAllocationLifetime.Session);
        }

        private void RecordScatterCullTelemetry(int totalCount, int frustumCount, int occlusionCount, int visibleCount)
        {
            int frameIndex = Time.frameCount;
            if (_lastScatterCullTelemetryFrame == frameIndex)
                return;

            _lastScatterCullTelemetryFrame = frameIndex;
            EnsureScatterCullTelemetry();
            if (!_scatterCullTelemetry.IsCreated)
                return;

            bool invalidCounterState =
                totalCount < 0 ||
                frustumCount < 0 ||
                occlusionCount < 0 ||
                visibleCount < 0 ||
                visibleCount > totalCount + _resolvedDensityDecimationStep;
            _lastCullOverdrawWarning = visibleCount > ScatterCullOverdrawWarningVisibleCount;

            _scatterCullTelemetry[_scatterCullTelemetryCursor] = new ScatterCullTelemetryEntry
            {
                FrameIndex = frameIndex,
                TotalInstances = totalCount,
                FrustumCulledCount = frustumCount,
                OcclusionCulledCount = occlusionCount,
                VisibleCount = visibleCount,
                DensityDecimationStep = _resolvedDensityDecimationStep,
                OverdrawWarning = _lastCullOverdrawWarning ? 1 : 0,
                SystemStress01 = _cachedSystemStress01,
                MaxDensity01 = _maxDensity01
            };
            _scatterCullTelemetryCursor = (_scatterCullTelemetryCursor + 1) % ScatterCullTelemetryFrameCount;

            if (invalidCounterState && !_scatterCullTelemetryDumped)
                DumpScatterCullTelemetry();
        }

        private void ReleaseScatterCullTelemetryResources()
        {
            if (_scatterCullTelemetry.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_scatterCullTelemetry);
                _scatterCullTelemetry.Dispose();
            }

            _scatterCullTelemetryCursor = 0;
            _lastScatterCullTelemetryFrame = -1;
            _lastScatterCullTelemetrySampleFrame = -1;
            _scatterCullTelemetryReadbackPending = false;
        }

        private void DumpScatterCullTelemetry()
        {
            _scatterCullTelemetryDumped = true;
            if (!_scatterCullTelemetry.IsCreated)
                return;

            TryWriteScatterCullTelemetryDump(ScatterCullDumpRelativePath);
            TryWriteScatterCullTelemetryDump(ScatterCullH8DumpRelativePath);
        }

        private void TryWriteScatterCullTelemetryDump(string relativePath)
        {
            try
            {
                string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                string dumpPath = Path.Combine(projectRoot, relativePath);
                string dumpDirectory = Path.GetDirectoryName(dumpPath);
                if (!string.IsNullOrEmpty(dumpDirectory))
                    Directory.CreateDirectory(dumpDirectory);

                using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(ScatterCullTelemetryFrameCount);
                    writer.Write(_scatterCullTelemetryCursor);
                    writer.Write(_instanceCount);

                    for (int offset = 0; offset < ScatterCullTelemetryFrameCount; offset++)
                    {
                        int readIndex = (_scatterCullTelemetryCursor + offset) % ScatterCullTelemetryFrameCount;
                        ScatterCullTelemetryEntry entry = _scatterCullTelemetry[readIndex];
                        writer.Write(entry.FrameIndex);
                        writer.Write(entry.TotalInstances);
                        writer.Write(entry.FrustumCulledCount);
                        writer.Write(entry.OcclusionCulledCount);
                        writer.Write(entry.VisibleCount);
                        writer.Write(entry.DensityDecimationStep);
                        writer.Write(entry.OverdrawWarning);
                        writer.Write(entry.SystemStress01);
                        writer.Write(entry.MaxDensity01);
                        writer.Write(entry.Reserved0);
                    }
                }
            }
            catch (Exception exception)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError($"[HectonIndirectVegetationRenderer] Failed to dump scatter cull telemetry: {exception.Message}", this);
#endif
            }
        }

        private static int ClampCounterToInt(uint counter)
        {
            return counter > int.MaxValue ? int.MaxValue : (int)counter;
        }

        private bool ClearIndirectArgsBuffer(GraphicsBuffer argsBuffer, Mesh mesh)
        {
            if (_cullingCompute == null ||
                _clearIndirectArgsKernel < 0 ||
                argsBuffer == null ||
                mesh == null)
            {
                return false;
            }

            bool sameShaderKernel = _indirectArgsClearBindingState.IsValidFlag != 0 &&
                                    ReferenceEquals(_indirectArgsClearBindingState.Shader, _cullingCompute) &&
                                    _indirectArgsClearBindingState.Kernel == _clearIndirectArgsKernel;
            bool sameArgsBuffer = sameShaderKernel &&
                                  ReferenceEquals(_indirectArgsClearBindingState.ArgsBuffer, argsBuffer);
            bool sameMeshConstants = sameShaderKernel &&
                                     ReferenceEquals(_indirectArgsClearBindingState.Mesh, mesh) &&
                                     _indirectArgsClearBindingState.SubMeshIndex == _subMeshIndex;

            if (!sameArgsBuffer)
                _cullingCompute.SetBuffer(_clearIndirectArgsKernel, _IndirectArgsBufferId, argsBuffer);

            int indexCountPerInstance;
            int startIndex;
            int baseVertexIndex;
            if (sameMeshConstants)
            {
                indexCountPerInstance = _indirectArgsClearBindingState.IndexCountPerInstance;
                startIndex = _indirectArgsClearBindingState.StartIndex;
                baseVertexIndex = _indirectArgsClearBindingState.BaseVertexIndex;
            }
            else
            {
                uint meshIndexCount = mesh.GetIndexCount(_subMeshIndex);
                uint meshStartIndex = mesh.GetIndexStart(_subMeshIndex);
                uint meshBaseVertexIndex = mesh.GetBaseVertex(_subMeshIndex);
                indexCountPerInstance = meshIndexCount > int.MaxValue ? int.MaxValue : (int)meshIndexCount;
                startIndex = meshStartIndex > int.MaxValue ? int.MaxValue : (int)meshStartIndex;
                baseVertexIndex = meshBaseVertexIndex > int.MaxValue ? int.MaxValue : (int)meshBaseVertexIndex;
                _cullingCompute.SetInt(_IndirectIndexCountPerInstanceId, indexCountPerInstance);
                _cullingCompute.SetInt(_IndirectStartIndexId, startIndex);
                _cullingCompute.SetInt(_IndirectBaseVertexIndexId, baseVertexIndex);
            }

            _indirectArgsClearBindingState = new IndirectArgsClearBindingState
            {
                Shader = _cullingCompute,
                Kernel = _clearIndirectArgsKernel,
                ArgsBuffer = argsBuffer,
                Mesh = mesh,
                SubMeshIndex = _subMeshIndex,
                IndexCountPerInstance = indexCountPerInstance,
                StartIndex = startIndex,
                BaseVertexIndex = baseVertexIndex,
                IsValidFlag = BindingFlagTrue
            };
            _cullingCompute.Dispatch(_clearIndirectArgsKernel, 1, 1, 1);
            return true;
        }

        private void PopulateFrustumPlaneUpload()
        {
            if (_frustumPlaneCache == null || _frustumPlaneVectors == null)
                return;

            for (int planeIndex = 0; planeIndex < FrustumPlaneCount; planeIndex++)
            {
                Plane plane = _frustumPlaneCache[planeIndex];
                _frustumPlaneVectors[planeIndex] = new Vector4(plane.normal.x, plane.normal.y, plane.normal.z, plane.distance);
            }
        }

        private void ReleaseGpuIndirectResources()
        {
            ReleaseVisibleIndexBuffer(ref _visibleIndicesLod0Buffer);
            ReleaseVisibleIndexBuffer(ref _visibleIndicesLod1Buffer);
            ReleaseVisibleIndexBuffer(ref _visibleIndicesShadowBuffer);
            ReleaseFloraSnapFlagBuffer();
            ReleaseGraphicsBuffer(ref _indirectArgsLod0Buffer);
            ReleaseGraphicsBuffer(ref _indirectArgsLod1Buffer);
            ReleaseGraphicsBuffer(ref _indirectArgsShadowBuffer);
            ReleaseGraphicsBuffer(ref _cullTelemetryCountersBuffer);
            ReleaseDepthPyramidTexture();
            _gpuVisibleIndexCapacity = 0;
            _gpuCullingFrameIndex = 0;
            _hasFarCullingSnapshot = false;
            _scatterCullTelemetryReadbackPending = false;
            _depthPyramidWidth = 0;
            _depthPyramidHeight = 0;
            _depthPyramidMipCount = 0;
            ResetCullComputeBindingStates();
            ResetSnapComputeBindingStates();
        }

        private void ResetCullComputeBindingStates()
        {
            _mainCullComputeBindingState = default;
            _shadowCullComputeBindingState = default;
            _indirectArgsClearBindingState = default;
        }

        private void ResetSnapComputeBindingStates()
        {
            _clearSnapComputeBindingState = default;
            _flagSnapComputeBindingState = default;
        }

        private static void ReleaseVisibleIndexBuffer(ref GraphicsBuffer buffer)
        {
            ReleaseGraphicsBuffer(ref buffer);
        }

        private static void ReleaseGraphicsBuffer(ref GraphicsBuffer buffer)
        {
            if (buffer == null)
                return;

            buffer.Release();
            buffer = null;
        }

        private void EnsureIndirectPropertyBlocks()
        {
            if (_nearIndirectProperties == null)
                _nearIndirectProperties = new MaterialPropertyBlock(); // COLD ALLOC: MaterialPropertyBlock[1] - near indirect vegetation pass property payload - owner: HectonIndirectVegetationRenderer
            if (_farIndirectProperties == null)
                _farIndirectProperties = new MaterialPropertyBlock(); // COLD ALLOC: MaterialPropertyBlock[1] - far indirect vegetation pass property payload - owner: HectonIndirectVegetationRenderer
            if (_depthNearIndirectProperties == null)
                _depthNearIndirectProperties = new MaterialPropertyBlock(); // COLD ALLOC: MaterialPropertyBlock[1] - depth near indirect vegetation pass property payload - owner: HectonIndirectVegetationRenderer
            if (_depthFarIndirectProperties == null)
                _depthFarIndirectProperties = new MaterialPropertyBlock(); // COLD ALLOC: MaterialPropertyBlock[1] - depth far indirect vegetation pass property payload - owner: HectonIndirectVegetationRenderer
            if (_shadowIndirectProperties == null)
                _shadowIndirectProperties = new MaterialPropertyBlock(); // COLD ALLOC: MaterialPropertyBlock[1] - shadow indirect vegetation pass property payload - owner: HectonIndirectVegetationRenderer
            if (_motionNearIndirectProperties == null)
                _motionNearIndirectProperties = new MaterialPropertyBlock(); // COLD ALLOC: MaterialPropertyBlock[1] - motion near indirect vegetation pass property payload - owner: HectonIndirectVegetationRenderer
            if (_motionFarIndirectProperties == null)
                _motionFarIndirectProperties = new MaterialPropertyBlock(); // COLD ALLOC: MaterialPropertyBlock[1] - motion far indirect vegetation pass property payload - owner: HectonIndirectVegetationRenderer
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

        private void ClearPassMaterialReference(ref Material target)
        {
            if (target == null)
                return;

            ResetMaterialBindingState(target);
            ReleaseRegisteredBrgMaterial(target);
            target = null;
        }

        private void ResetMaterialBindingState(Material material)
        {
            if (ReferenceEquals(_nearMaterialBindingState.Material, material))
                _nearMaterialBindingState = default;
            if (ReferenceEquals(_farMaterialBindingState.Material, material))
                _farMaterialBindingState = default;
            if (ReferenceEquals(_depthNearMaterialBindingState.Material, material))
                _depthNearMaterialBindingState = default;
            if (ReferenceEquals(_depthFarMaterialBindingState.Material, material))
                _depthFarMaterialBindingState = default;
            if (ReferenceEquals(_shadowMaterialBindingState.Material, material))
                _shadowMaterialBindingState = default;
            if (ReferenceEquals(_motionNearMaterialBindingState.Material, material))
                _motionNearMaterialBindingState = default;
            if (ReferenceEquals(_motionFarMaterialBindingState.Material, material))
                _motionFarMaterialBindingState = default;
            if (ReferenceEquals(_motionNearPreviousCameraBindingState.Material, material))
                _motionNearPreviousCameraBindingState = default;
            if (ReferenceEquals(_motionFarPreviousCameraBindingState.Material, material))
                _motionFarPreviousCameraBindingState = default;
        }

        private void ReleaseRegisteredBrgMaterial(Material material)
        {
            if (material == null)
                return;

            if (ReferenceEquals(_registeredNearBrgMaterial, material))
            {
                UnregisterBatchMaterial(ref _nearBatchMaterialId);
                _registeredNearBrgMaterial = null;
            }

            if (ReferenceEquals(_registeredFarBrgMaterial, material))
            {
                UnregisterBatchMaterial(ref _farBatchMaterialId);
                _registeredFarBrgMaterial = null;
            }

            if (ReferenceEquals(_registeredDepthNearBrgMaterial, material))
            {
                UnregisterBatchMaterial(ref _depthNearBatchMaterialId);
                _registeredDepthNearBrgMaterial = null;
            }

            if (ReferenceEquals(_registeredDepthFarBrgMaterial, material))
            {
                UnregisterBatchMaterial(ref _depthFarBatchMaterialId);
                _registeredDepthFarBrgMaterial = null;
            }

            if (ReferenceEquals(_registeredShadowBrgMaterial, material))
            {
                UnregisterBatchMaterial(ref _shadowBatchMaterialId);
                _registeredShadowBrgMaterial = null;
            }

            if (ReferenceEquals(_registeredMotionNearBrgMaterial, material))
            {
                UnregisterBatchMaterial(ref _motionNearBatchMaterialId);
                _registeredMotionNearBrgMaterial = null;
            }

            if (ReferenceEquals(_registeredMotionFarBrgMaterial, material))
            {
                UnregisterBatchMaterial(ref _motionFarBatchMaterialId);
                _registeredMotionFarBrgMaterial = null;
            }
        }

        private void SyncBatchRegistration(Mesh nearMesh, Mesh farMesh)
        {
            if (_batchRendererGroup == null)
                return;

            SyncBatchMesh(ref _nearBatchMeshId, ref _registeredNearMesh, nearMesh);
            SyncBatchMesh(ref _farBatchMeshId, ref _registeredFarMesh, farMesh);
            SyncBatchMaterial(ref _nearBatchMaterialId, ref _registeredNearBrgMaterial, _nearBrgMaterial);
            SyncBatchMaterial(ref _farBatchMaterialId, ref _registeredFarBrgMaterial, _farBrgMaterial);
            SyncBatchMaterial(ref _depthNearBatchMaterialId, ref _registeredDepthNearBrgMaterial, _depthNearBrgMaterial);
            SyncBatchMaterial(ref _depthFarBatchMaterialId, ref _registeredDepthFarBrgMaterial, _depthFarBrgMaterial);
            SyncBatchMaterial(ref _shadowBatchMaterialId, ref _registeredShadowBrgMaterial, _shadowBrgMaterial);
            SyncBatchMaterial(ref _motionNearBatchMaterialId, ref _registeredMotionNearBrgMaterial, _motionNearBrgMaterial);
            SyncBatchMaterial(ref _motionFarBatchMaterialId, ref _registeredMotionFarBrgMaterial, _motionFarBrgMaterial);
        }

        private void SyncBatchMesh(ref BatchMeshID batchMeshId, ref Mesh registeredMesh, Mesh mesh)
        {
            if (_batchRendererGroup == null)
                return;

            if (registeredMesh == mesh)
                return;

            if (batchMeshId.value != 0u)
                _batchRendererGroup.UnregisterMesh(batchMeshId);

            batchMeshId = mesh != null ? _batchRendererGroup.RegisterMesh(mesh) : default;
            registeredMesh = mesh;
        }

        private void SyncBatchMaterial(ref BatchMaterialID batchMaterialId, ref Material registeredMaterial, Material material)
        {
            if (_batchRendererGroup == null)
                return;

            if (registeredMaterial == material)
                return;

            if (batchMaterialId.value != 0u)
                _batchRendererGroup.UnregisterMaterial(batchMaterialId);

            batchMaterialId = material != null ? _batchRendererGroup.RegisterMaterial(material) : default;
            registeredMaterial = material;
        }

        private void SyncBatchBuffer(GraphicsBuffer matrixBuffer)
        {
            if (_batchRendererGroup == null || _batchId.value == 0u || matrixBuffer == null)
                return;

            if (ReferenceEquals(_registeredBatchBuffer, matrixBuffer))
                return;

            _batchRendererGroup.SetBatchBuffer(_batchId, matrixBuffer.bufferHandle);
            _registeredBatchBuffer = matrixBuffer;
        }

        private void UpdateMotionVectorHistory(Camera renderCamera, Vector3 currentCameraPosition)
        {
            if (_motionNearBrgMaterial == null && _motionFarBrgMaterial == null)
                return;

            if (renderCamera == null)
                return;

            Vector3 previousCameraPosition = _hasPreviousMotionCameraPosition && _previousMotionCamera == renderCamera
                ? _previousMotionCameraPosition
                : currentCameraPosition;

            ApplyMotionVectorPreviousCamera(
                _motionNearIndirectProperties,
                _motionNearBrgMaterial,
                ref _motionNearPreviousCameraBindingState,
                previousCameraPosition);
            ApplyMotionVectorPreviousCamera(
                _motionFarIndirectProperties,
                _motionFarBrgMaterial,
                ref _motionFarPreviousCameraBindingState,
                previousCameraPosition);

            _previousMotionCameraPosition = currentCameraPosition;
            _previousMotionCamera = renderCamera;
            _hasPreviousMotionCameraPosition = true;
        }

        private void ApplyMotionVectorPreviousCamera(
            MaterialPropertyBlock propertyBlock,
            Material material,
            ref MaterialVectorBindingState state,
            Vector3 previousCameraPosition)
        {
            if (propertyBlock == null || material == null)
                return;

            if (state.IsValidFlag != 0 &&
                ReferenceEquals(state.Material, material) &&
                state.Value.x == previousCameraPosition.x &&
                state.Value.y == previousCameraPosition.y &&
                state.Value.z == previousCameraPosition.z)
            {
                return;
            }

            propertyBlock.SetVector(_PreviousCameraPositionId, previousCameraPosition);
            state = new MaterialVectorBindingState
            {
                Material = material,
                Value = previousCameraPosition,
                IsValidFlag = BindingFlagTrue
            };
        }

        private void EnsureCpuCullingCapacity(int instanceCount)
        {
            if (instanceCount <= 0)
                return;

            RetireCompletedCpuCullingDisposeHandles();
            int nextCapacity = Mathf.NextPowerOfTwo(Mathf.Max(16, instanceCount));
            if (_cpuCullingMatrices.IsCreated &&
                _cpuCullingMatrices.Length >= nextCapacity &&
                _cpuCullingData.IsCreated &&
                _cpuCullingData.Length >= nextCapacity)
            {
                return;
            }

            ReleaseCpuCullingData();
            _cpuCullingMatrices = new NativeArray<Matrix4x4>(nextCapacity, DataVaultExemptVegetationCpuCullingAllocator, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<Matrix4x4>[NextPowerOfTwo(requiredCount)] - CPU BRG vegetation culling matrices - owner: HectonIndirectVegetationRenderer
            _cpuCullingData = new NativeArray<HectonVegetationInstanceData>(nextCapacity, DataVaultExemptVegetationCpuCullingAllocator, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<HectonVegetationInstanceData>[NextPowerOfTwo(requiredCount)] - CPU BRG vegetation culling metadata - owner: HectonIndirectVegetationRenderer
            NativeMemorySentinel.RegisterNativeArray(_cpuCullingMatrices, nameof(HectonIndirectVegetationRenderer), nameof(_cpuCullingMatrices), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(_cpuCullingData, nameof(HectonIndirectVegetationRenderer), nameof(_cpuCullingData), NativeAllocationLifetime.Session);
        }

        private void ReleaseCpuCullingData()
        {
            bool hasCullDependency = TryGetCpuCullingScratchDependency(out JobHandle cullDependency);
            DisposeCpuCullingDataArray(ref _cpuCullingMatrices, cullDependency, hasCullDependency);
            DisposeCpuCullingDataArray(ref _cpuCullingData, cullDependency, hasCullDependency);

            _hasCpuCullingData = false;
        }

        private bool TryGetCpuCullingScratchDependency(out JobHandle dependency)
        {
            dependency = default;
            bool hasDependency = false;

            if (_cpuCullingScratchA.ActiveHandleValidFlag != 0)
            {
                dependency = _cpuCullingScratchA.ActiveHandle;
                hasDependency = true;
            }

            if (_cpuCullingScratchB.ActiveHandleValidFlag != 0)
            {
                dependency = hasDependency
                    ? JobHandle.CombineDependencies(dependency, _cpuCullingScratchB.ActiveHandle)
                    : _cpuCullingScratchB.ActiveHandle;
                hasDependency = true;
            }

            return hasDependency;
        }

        private void RetireCompletedCpuCullingDisposeHandles()
        {
            if (_cpuCullingDataDisposeHandleValid && _cpuCullingDataDisposeHandle.IsCompleted)
            {
                DispatcherJobFence.TryFinalizeCompleted(ref _cpuCullingDataDisposeHandle);
                _cpuCullingDataDisposeHandleValid = false;
            }

            if (_cpuCullingScratchDisposeHandleValid && _cpuCullingScratchDisposeHandle.IsCompleted)
            {
                DispatcherJobFence.TryFinalizeCompleted(ref _cpuCullingScratchDisposeHandle);
                _cpuCullingScratchDisposeHandleValid = false;
            }
        }

        private void DisposeCpuCullingDataArray<T>(ref NativeArray<T> array, JobHandle dependency, bool hasDependency)
            where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(array);
            if (!hasDependency)
            {
                array.Dispose();
            }
            else
            {
                AppendCpuCullingDataDisposeHandle(array.Dispose(dependency));
            }

            array = default;
        }

        private void AppendCpuCullingDataDisposeHandle(JobHandle disposeHandle)
        {
            _cpuCullingDataDisposeHandle = _cpuCullingDataDisposeHandleValid
                ? JobHandle.CombineDependencies(_cpuCullingDataDisposeHandle, disposeHandle)
                : disposeHandle;
            _cpuCullingDataDisposeHandleValid = true;
        }

        private bool TryPrepareCpuCullingScratch(int instanceCount, out int scratchIndex)
        {
            RetireCompletedCpuCullingDisposeHandles();
            for (int attempt = 0; attempt < CpuCullingScratchBufferCount; attempt++)
            {
                int candidateIndex = (_cpuCullingScratchCursor + attempt) & 1;
                ref CpuCullingScratchBuffer candidate = ref GetCpuCullingScratch(candidateIndex);
                if (candidate.ActiveHandleValidFlag != 0 && !candidate.ActiveHandle.IsCompleted)
                    continue;

                if (candidate.ActiveHandleValidFlag != 0)
                {
                    DispatcherJobFence.TryFinalizeCompleted(ref candidate.ActiveHandle);
                    candidate.ActiveHandleValidFlag = BindingFlagFalse;
                }

                EnsureCpuCullingScratchCapacity(ref candidate, candidateIndex, instanceCount);
                if (!candidate.VisibilityMask.IsCreated ||
                    !candidate.CullingPlanes.IsCreated ||
                    candidate.VisibilityCapacity < instanceCount)
                {
                    scratchIndex = -1;
                    return false;
                }

                _cpuCullingScratchCursor = (candidateIndex + 1) & 1;
                scratchIndex = candidateIndex;
                return true;
            }

            scratchIndex = -1;
            return false;
        }

        private ref CpuCullingScratchBuffer GetCpuCullingScratch(int scratchIndex)
        {
            if ((scratchIndex & 1) == 0)
                return ref _cpuCullingScratchA;

            return ref _cpuCullingScratchB;
        }

        private void EnsureCpuCullingScratchCapacity(ref CpuCullingScratchBuffer scratch, int scratchIndex, int instanceCount)
        {
            int nextCapacity = Mathf.NextPowerOfTwo(Mathf.Max(16, instanceCount));
            if (scratch.VisibilityMask.IsCreated &&
                scratch.VisibilityCapacity >= nextCapacity &&
                scratch.CullingPlanes.IsCreated &&
                scratch.HeadlightPositionsWs.IsCreated &&
                scratch.HeadlightDirectionsWs.IsCreated &&
                scratch.HeadlightColors.IsCreated &&
                scratch.HeadlightConeData.IsCreated)
            {
                return;
            }

            ReleaseCpuCullingScratch(ref scratch, deferActiveJobs: false);
            scratch.VisibilityMask = new NativeArray<byte>(nextCapacity, DataVaultExemptVegetationCpuScratchAllocator, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<byte>[NextPowerOfTwo(instanceCount)] - BRG fallback visibility scratch - owner: HectonIndirectVegetationRenderer
            scratch.CullingPlanes = new NativeArray<float4>(CpuCullingScratchPlaneCapacity, DataVaultExemptVegetationCpuScratchAllocator, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<float4>[16] - BRG fallback culling plane scratch - owner: HectonIndirectVegetationRenderer
            scratch.HeadlightPositionsWs = new NativeArray<float4>(MaxScooterHeadlights, DataVaultExemptVegetationCpuScratchAllocator, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<float4>[2] - BRG fallback headlight position scratch - owner: HectonIndirectVegetationRenderer
            scratch.HeadlightDirectionsWs = new NativeArray<float4>(MaxScooterHeadlights, DataVaultExemptVegetationCpuScratchAllocator, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<float4>[2] - BRG fallback headlight direction scratch - owner: HectonIndirectVegetationRenderer
            scratch.HeadlightColors = new NativeArray<float4>(MaxScooterHeadlights, DataVaultExemptVegetationCpuScratchAllocator, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<float4>[2] - BRG fallback headlight color scratch - owner: HectonIndirectVegetationRenderer
            scratch.HeadlightConeData = new NativeArray<float4>(MaxScooterHeadlights, DataVaultExemptVegetationCpuScratchAllocator, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<float4>[2] - BRG fallback headlight cone scratch - owner: HectonIndirectVegetationRenderer
            scratch.VisibilityCapacity = nextCapacity;
            scratch.ActiveHandle = default;
            scratch.ActiveHandleValidFlag = BindingFlagFalse;

            bool firstScratch = scratchIndex == 0;
            NativeMemorySentinel.RegisterNativeArray(scratch.VisibilityMask, nameof(HectonIndirectVegetationRenderer), firstScratch ? "CpuCullingScratchA.VisibilityMask" : "CpuCullingScratchB.VisibilityMask", NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(scratch.CullingPlanes, nameof(HectonIndirectVegetationRenderer), firstScratch ? "CpuCullingScratchA.CullingPlanes" : "CpuCullingScratchB.CullingPlanes", NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(scratch.HeadlightPositionsWs, nameof(HectonIndirectVegetationRenderer), firstScratch ? "CpuCullingScratchA.HeadlightPositionsWs" : "CpuCullingScratchB.HeadlightPositionsWs", NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(scratch.HeadlightDirectionsWs, nameof(HectonIndirectVegetationRenderer), firstScratch ? "CpuCullingScratchA.HeadlightDirectionsWs" : "CpuCullingScratchB.HeadlightDirectionsWs", NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(scratch.HeadlightColors, nameof(HectonIndirectVegetationRenderer), firstScratch ? "CpuCullingScratchA.HeadlightColors" : "CpuCullingScratchB.HeadlightColors", NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(scratch.HeadlightConeData, nameof(HectonIndirectVegetationRenderer), firstScratch ? "CpuCullingScratchA.HeadlightConeData" : "CpuCullingScratchB.HeadlightConeData", NativeAllocationLifetime.Session);
        }

        private void ReleaseCpuCullingScratchBuffers(bool deferActiveJobs)
        {
            ReleaseCpuCullingScratch(ref _cpuCullingScratchA, deferActiveJobs);
            ReleaseCpuCullingScratch(ref _cpuCullingScratchB, deferActiveJobs);
            _cpuCullingScratchCursor = 0;
        }

        private void ReleaseCpuCullingScratch(ref CpuCullingScratchBuffer scratch, bool deferActiveJobs)
        {
            JobHandle disposeDependency = default;
            bool hasDisposeDependency = false;
            if (scratch.ActiveHandleValidFlag != 0)
            {
                if (deferActiveJobs && !scratch.ActiveHandle.IsCompleted)
                {
                    disposeDependency = scratch.ActiveHandle;
                    hasDisposeDependency = true;
                }
                else
                {
                    DispatcherJobFence.TryComplete(ref scratch.ActiveHandle, forceComplete: true);
                }

                scratch.ActiveHandleValidFlag = BindingFlagFalse;
            }

            DisposeCpuCullingScratchArray(ref scratch.VisibilityMask, disposeDependency, hasDisposeDependency);
            DisposeCpuCullingScratchArray(ref scratch.CullingPlanes, disposeDependency, hasDisposeDependency);
            DisposeCpuCullingScratchArray(ref scratch.HeadlightPositionsWs, disposeDependency, hasDisposeDependency);
            DisposeCpuCullingScratchArray(ref scratch.HeadlightDirectionsWs, disposeDependency, hasDisposeDependency);
            DisposeCpuCullingScratchArray(ref scratch.HeadlightColors, disposeDependency, hasDisposeDependency);
            DisposeCpuCullingScratchArray(ref scratch.HeadlightConeData, disposeDependency, hasDisposeDependency);

            scratch = default;
        }

        private void DisposeCpuCullingScratchArray<T>(ref NativeArray<T> array, JobHandle dependency, bool hasDependency)
            where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(array);
            if (!hasDependency)
            {
                array.Dispose();
            }
            else
            {
                AppendCpuCullingScratchDisposeHandle(array.Dispose(dependency));
            }

            array = default;
        }

        private void AppendCpuCullingScratchDisposeHandle(JobHandle disposeHandle)
        {
            _cpuCullingScratchDisposeHandle = _cpuCullingScratchDisposeHandleValid
                ? JobHandle.CombineDependencies(_cpuCullingScratchDisposeHandle, disposeHandle)
                : disposeHandle;
            _cpuCullingScratchDisposeHandleValid = true;
        }

#if UNITY_EDITOR
        private void ReleaseMockScatterBuffers()
        {
            if (_mockScatterMatrices.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeList(nameof(HectonIndirectVegetationRenderer), nameof(_mockScatterMatrices));
                _mockScatterMatrices.Dispose();
            }

            if (_mockScatterData.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeList(nameof(HectonIndirectVegetationRenderer), nameof(_mockScatterData));
                _mockScatterData.Dispose();
            }
        }
#endif

        private void CopyCpuCullingPayload(
            Matrix4x4[] instanceMatrices,
            HectonVegetationInstanceData[] instanceData,
            int instanceCount)
        {
            if (instanceMatrices == null || instanceCount <= 0 || instanceMatrices.Length < instanceCount)
            {
                _hasCpuCullingData = false;
                _floraAgesAuthoredExternally = false;
                _floraAgeBufferDirty = true;
                return;
            }

            EnsureCpuCullingCapacity(instanceCount);
            EnsureFloraAgeCapacity(instanceCount);
            HectonVegetationInstanceData fallbackPayload = CreateLegacyDefaultPayload();
            for (int instanceIndex = 0; instanceIndex < instanceCount; instanceIndex++)
            {
                _cpuCullingMatrices[instanceIndex] = instanceMatrices[instanceIndex];
                HectonVegetationInstanceData metadata = instanceData != null && instanceData.Length > instanceIndex
                    ? instanceData[instanceIndex]
                    : fallbackPayload;
                _cpuCullingData[instanceIndex] = metadata;
                _floraAges01[instanceIndex] = ResolveFloraAgeFromMetadata(metadata);
            }

            _hasCpuCullingData = true;
            _floraAgesAuthoredExternally = false;
            _floraAgeBufferDirty = true;
        }

        private void CopyCpuCullingPayload(
            NativeArray<Matrix4x4> instanceMatrices,
            NativeArray<HectonVegetationInstanceData> instanceData,
            int instanceCount)
        {
            if (!instanceMatrices.IsCreated || !instanceData.IsCreated || instanceCount <= 0)
            {
                _hasCpuCullingData = false;
                _floraAgesAuthoredExternally = false;
                _floraAgeBufferDirty = true;
                return;
            }

            EnsureCpuCullingCapacity(instanceCount);
            EnsureFloraAgeCapacity(instanceCount);
            NativeArray<Matrix4x4>.Copy(instanceMatrices, _cpuCullingMatrices, instanceCount);
            NativeArray<HectonVegetationInstanceData>.Copy(instanceData, _cpuCullingData, instanceCount);
            for (int instanceIndex = 0; instanceIndex < instanceCount; instanceIndex++)
                _floraAges01[instanceIndex] = ResolveFloraAgeFromMetadata(instanceData[instanceIndex]);
            _hasCpuCullingData = true;
            _floraAgesAuthoredExternally = false;
            _floraAgeBufferDirty = true;
        }

        private static void ResolveInstanceShape(
            in HectonVegetationInstanceData instanceData,
            out float instanceHeight,
            out float instanceWidth)
        {
            float instanceType = Mathf.Clamp(Mathf.Round(instanceData.Type), 0f, 2f);
            float encodedHeightScale = Mathf.Clamp01(Mathf.Abs(instanceData.HeightScale));
            float encodedWidthScale = instanceData.WidthScale < 0f ? 1f : Mathf.Clamp01(instanceData.WidthScale);
            if (instanceType < 0.5f)
            {
                instanceHeight = math.lerp(0.35f, 1.4f, encodedHeightScale);
                instanceWidth = math.lerp(0.65f, 1.25f, encodedWidthScale);
                return;
            }

            if (instanceType < 1.5f)
            {
                instanceHeight = math.lerp(10f, 20f, encodedHeightScale);
                instanceWidth = math.lerp(0.55f, 1.6f, encodedWidthScale);
                return;
            }

            instanceHeight = math.lerp(0.75f, 2.4f, encodedHeightScale);
            instanceWidth = math.lerp(0.75f, 1.35f, encodedWidthScale);
        }

        private static Vector3 TransformPoint(Matrix4x4 matrixValue, float x, float y, float z)
        {
            return matrixValue.MultiplyPoint3x4(new Vector3(x, y, z));
        }

        private bool IsVisibleInDarkness(Vector3 samplePositionWS)
        {
            if (!_enableDarknessCulling)
                return true;

            float globalBiolum = Mathf.Max(
                ResolveBiolumIntensityScalar(),
                Mathf.Max(
                    Shader.GetGlobalFloat(_FloorBiolumStrengthId),
                    Shader.GetGlobalFloat(_OceanBiolumStrengthId)));
            if (globalBiolum >= _darknessBiolumThreshold)
                return true;

            int headlightCount = CopyScooterHeadlightPayload();
            for (int headlightIndex = 0; headlightIndex < headlightCount; headlightIndex++)
            {
                Vector4 lightPosition = _scooterHeadlightPositionsWs[headlightIndex];
                float lightRange = Mathf.Max(0.1f, lightPosition.w);
                float3 toSample = new float3(
                    samplePositionWS.x - lightPosition.x,
                    samplePositionWS.y - lightPosition.y,
                    samplePositionWS.z - lightPosition.z);
                float sampleDistanceSq = math.lengthsq(toSample);
                float lightRangeSq = lightRange * lightRange;
                if (sampleDistanceSq >= lightRangeSq || sampleDistanceSq <= 0.00000001f)
                    continue;

                Vector4 directionData = _scooterHeadlightDirectionsWs[headlightIndex];
                float3 lightDirection = new float3(directionData.x, directionData.y, directionData.z);
                float outerCos = _scooterHeadlightConeData[headlightIndex].x;
                float dotLight = math.dot(lightDirection, toSample);
                if (!PassesDotThresholdSq(dotLight, outerCos, sampleDistanceSq))
                    continue;

                float invRange = _scooterHeadlightConeData[headlightIndex].z;
                float rangeAttenuation = math.saturate(1f - sampleDistanceSq * invRange * invRange);
                rangeAttenuation *= rangeAttenuation;
                float intensity = _scooterHeadlightColors[headlightIndex].w * _scooterHeadlightConeData[headlightIndex].y;
                if (rangeAttenuation * intensity >= 0.02f)
                    return true;
            }

            return false;
        }

        private static bool PassesDotThresholdSq(float dotValue, float threshold, float lengthProductSq)
        {
            if (!math.isfinite(dotValue) || !math.isfinite(threshold) || !math.isfinite(lengthProductSq) || lengthProductSq <= 0.00000001f)
                return true;

            float thresholdSq = threshold * threshold;
            float dotSq = dotValue * dotValue;
            return threshold >= 0f
                ? dotValue >= 0f && dotSq >= thresholdSq * lengthProductSq
                : dotValue >= 0f || dotSq <= thresholdSq * lengthProductSq;
        }

        private JobHandle OnPerformCulling(
            BatchRendererGroup rendererGroup,
            BatchCullingContext cullingContext,
            BatchCullingOutput cullingOutput,
            IntPtr userContext)
        {
            Mesh nearMesh = ResolveNearRenderMesh();
            Mesh farMesh = FrameTimeWatchdog.IsDistantFloraRenderingEnabled && _farLodDistance > _nearLodDistance
                ? ResolveImpostorRenderMesh()
                : null;
            bool useFarPass = farMesh != null && _farBatchMeshId.value != 0u && _farBatchMaterialId.value != 0u;
            bool useDepthPass = _enableDepthPrepass && _depthNearBrgMaterial != null && _depthNearBatchMaterialId.value != 0u;
            bool useShadowPass = _enableShadowCasterDraw && _shadowBrgMaterial != null && _shadowBatchMaterialId.value != 0u && HasMainDirectionalShadowLight();
            bool useMotionPass = _enableMotionVectorDraw && _motionNearBrgMaterial != null && _motionNearBatchMaterialId.value != 0u;

            if (_instanceCount <= 0 ||
                _batchId.value == 0u ||
                nearMesh == null ||
                _nearBatchMeshId.value == 0u ||
                _nearBatchMaterialId.value == 0u)
            {
                HectonBatchRendererGroupUtility.WriteDirectDrawOutput(
                    cullingOutput,
                    HectonBatchRendererGroupUtility.AllocateDirectDrawOutput(0, 0, 0));
                return default;
            }

            Bounds drawBounds = ResolveDrawBounds(transform.position);
            if (!HectonBatchRendererGroupUtility.IsBoundsVisible(cullingContext.cullingPlanes, drawBounds))
            {
                HectonBatchRendererGroupUtility.WriteDirectDrawOutput(
                    cullingOutput,
                    HectonBatchRendererGroupUtility.AllocateDirectDrawOutput(0, 0, 0));
                return default;
            }

            bool useDepthFarPass = useDepthPass && useFarPass && _depthFarBrgMaterial != null && _depthFarBatchMaterialId.value != 0u;
            bool useMotionFarPass = useMotionPass && useFarPass && _motionFarBrgMaterial != null && _motionFarBatchMaterialId.value != 0u;
            bool enableCpuCulling = _hasCpuCullingData &&
                                    _cpuCullingMatrices.IsCreated &&
                                    _cpuCullingData.IsCreated &&
                                    _cpuCullingMatrices.Length >= _instanceCount &&
                                    _cpuCullingData.Length >= _instanceCount;
            float brgLodDistanceScalar = VRAMPressureMonitor.BrgLodDistanceScalar;
            float lodTransition = Mathf.Max(_lodTransitionRange * brgLodDistanceScalar, 0.01f);
            float nearLodDistance = Mathf.Max(_nearLodDistance * brgLodDistanceScalar, 0.01f);
            float farLodDistance = Mathf.Max(nearLodDistance, _farLodDistance * brgLodDistanceScalar);
            float lod0MaxDistance = nearLodDistance + lodTransition;
            float lod1MinDistance = Mathf.Max(0f, nearLodDistance - lodTransition);
            float lod1MaxDistance = farLodDistance + lodTransition;
            Vector4 floatingOffset = ResolveVegetationFloatingOffset();
            int densityDecimationStep = ResolveDensityDecimationStep();
            _resolvedDensityDecimationStep = densityDecimationStep;

            if (!enableCpuCulling)
            {
                WriteAllVisibleVegetationOutput(
                    cullingOutput,
                    useFarPass,
                    useDepthPass,
                    useDepthFarPass,
                    useShadowPass,
                    useMotionPass,
                    useMotionFarPass);
                return default;
            }

            if (!TryPrepareCpuCullingScratch(_instanceCount, out int scratchIndex))
            {
                WriteAllVisibleVegetationOutput(
                    cullingOutput,
                    useFarPass,
                    useDepthPass,
                    useDepthFarPass,
                    useShadowPass,
                    useMotionPass,
                    useMotionFarPass);
                return default;
            }

            ref CpuCullingScratchBuffer scratch = ref GetCpuCullingScratch(scratchIndex);
            NativeArray<byte> visibilityMask = scratch.VisibilityMask;
            NativeArray<float4> cullingPlanes = scratch.CullingPlanes;
            NativeArray<float4> headlightPositionsWs = scratch.HeadlightPositionsWs;
            NativeArray<float4> headlightDirectionsWs = scratch.HeadlightDirectionsWs;
            NativeArray<float4> headlightColors = scratch.HeadlightColors;
            NativeArray<float4> headlightConeData = scratch.HeadlightConeData;
            bool bypassDarknessCulling = !_enableDarknessCulling;
            int cullingPlaneCount = 0;
            int headlightCount = 0;

            if (enableCpuCulling)
            {
                int planeCount = cullingContext.cullingPlanes.IsCreated ? cullingContext.cullingPlanes.Length : 0;
                if (planeCount > 0)
                {
                    int safePlaneCount = math.min(planeCount, cullingPlanes.Length);
                    for (int planeIndex = 0; planeIndex < safePlaneCount; planeIndex++)
                    {
                        Plane plane = cullingContext.cullingPlanes[planeIndex];
                        cullingPlanes[planeIndex] = new float4(plane.normal.x, plane.normal.y, plane.normal.z, plane.distance);
                    }
                    cullingPlaneCount = safePlaneCount;
                }

                if (_enableDarknessCulling)
                {
                    float globalBiolum = Mathf.Max(
                        ResolveBiolumIntensityScalar(),
                        Mathf.Max(
                            Shader.GetGlobalFloat(_FloorBiolumStrengthId),
                            Shader.GetGlobalFloat(_OceanBiolumStrengthId)));
                    if (globalBiolum >= _darknessBiolumThreshold)
                    {
                        bypassDarknessCulling = true;
                    }
                    else
                    {
                        bypassDarknessCulling = false;
                        headlightCount = CopyScooterHeadlightPayload();
                        if (headlightCount > 0)
                        {
                            for (int headlightIndex = 0; headlightIndex < MaxScooterHeadlights; headlightIndex++)
                            {
                                Vector4 lightPosition = _scooterHeadlightPositionsWs[headlightIndex];
                                Vector4 lightDirection = _scooterHeadlightDirectionsWs[headlightIndex];
                                Vector4 lightColor = _scooterHeadlightColors[headlightIndex];
                                Vector4 coneData = _scooterHeadlightConeData[headlightIndex];
                                headlightPositionsWs[headlightIndex] = new float4(lightPosition.x, lightPosition.y, lightPosition.z, lightPosition.w);
                                headlightDirectionsWs[headlightIndex] = new float4(lightDirection.x, lightDirection.y, lightDirection.z, lightDirection.w);
                                headlightColors[headlightIndex] = new float4(lightColor.x, lightColor.y, lightColor.z, lightColor.w);
                                headlightConeData[headlightIndex] = new float4(coneData.x, coneData.y, coneData.z, coneData.w);
                            }
                        }
                    }
                }
            }

            unsafe
            {
                int visibleInstanceCapacity = CalculateVegetationVisibleInstanceCapacity(
                    _instanceCount,
                    useFarPass,
                    useShadowPass);
                int drawCommandCapacity = CalculateVegetationDrawCommandCapacity(
                    useFarPass,
                    useDepthPass,
                    useDepthFarPass,
                    useShadowPass,
                    useMotionPass,
                    useMotionFarPass);
                FrameTimeWatchdog.ReportBatchRendererGroupBatchCount(drawCommandCapacity);
                BatchCullingOutputDrawCommands output = HectonBatchRendererGroupUtility.AllocateDirectDrawOutput(
                    visibleInstanceCapacity,
                    drawCommandCapacity,
                    drawCommandCapacity);

                JobHandle visibilityHandle = new BuildVegetationVisibilityMaskJob
                {
                    Matrices = _cpuCullingMatrices,
                    InstanceData = _cpuCullingData,
                    CullingPlanes = cullingPlanes,
                    HeadlightPositionsWs = headlightPositionsWs,
                    HeadlightDirectionsWs = headlightDirectionsWs,
                    HeadlightColors = headlightColors,
                    HeadlightConeData = headlightConeData,
                    VisibilityMask = visibilityMask,
                    InstanceCount = _instanceCount,
                    CullingPlaneCount = cullingPlaneCount,
                    HeadlightCount = headlightCount,
                    EnableCpuCullingFlag = enableCpuCulling ? (byte)1 : (byte)0,
                    UseFarPassFlag = useFarPass ? (byte)1 : (byte)0,
                    UseShadowPassFlag = useShadowPass ? (byte)1 : (byte)0,
                    BypassDarknessCullingFlag = bypassDarknessCulling ? (byte)1 : (byte)0,
                    DensityDecimationStep = densityDecimationStep,
                    ViewPosition = _cachedCullCameraPosition,
                    GlobalOffset = new float3(floatingOffset.x, floatingOffset.y, floatingOffset.z),
                    Lod0MaxDistanceSq = lod0MaxDistance * lod0MaxDistance,
                    Lod1MinDistanceSq = lod1MinDistance * lod1MinDistance,
                    Lod1MaxDistanceSq = lod1MaxDistance * lod1MaxDistance
                }.Schedule(_instanceCount, 64);

                JobHandle finalizeHandle = new FinalizeVegetationDrawOutputJob
                {
                    VisibilityMask = visibilityMask,
                    InstanceCount = _instanceCount,
                    Layer = gameObject.layer,
                    SubMeshIndex = _subMeshIndex,
                    UseFarPassFlag = useFarPass ? (byte)1 : (byte)0,
                    UseDepthPassFlag = useDepthPass ? (byte)1 : (byte)0,
                    UseDepthFarPassFlag = useDepthFarPass ? (byte)1 : (byte)0,
                    UseShadowPassFlag = useShadowPass ? (byte)1 : (byte)0,
                    UseMotionPassFlag = useMotionPass ? (byte)1 : (byte)0,
                    UseMotionFarPassFlag = useMotionFarPass ? (byte)1 : (byte)0,
                    BatchId = _batchId,
                    NearMeshId = _nearBatchMeshId,
                    FarMeshId = _farBatchMeshId,
                    NearMaterialId = _nearBatchMaterialId,
                    FarMaterialId = _farBatchMaterialId,
                    DepthNearMaterialId = _depthNearBatchMaterialId,
                    DepthFarMaterialId = _depthFarBatchMaterialId,
                    ShadowMaterialId = _shadowBatchMaterialId,
                    MotionNearMaterialId = _motionNearBatchMaterialId,
                    MotionFarMaterialId = _motionFarBatchMaterialId,
                    VisibleInstances = output.visibleInstances,
                    DrawCommands = output.drawCommands,
                    DrawRanges = output.drawRanges,
                    OutputCommands = (BatchCullingOutputDrawCommands*)NativeArrayUnsafeUtility.GetUnsafePtr(cullingOutput.drawCommands)
                }.Schedule(visibilityHandle);

                scratch.ActiveHandle = finalizeHandle;
                scratch.ActiveHandleValidFlag = BindingFlagTrue;
                return finalizeHandle;
            }
        }

        private unsafe void WriteAllVisibleVegetationOutput(
            BatchCullingOutput cullingOutput,
            bool useFarPass,
            bool useDepthPass,
            bool useDepthFarPass,
            bool useShadowPass,
            bool useMotionPass,
            bool useMotionFarPass)
        {
            int nearOffset = 0;
            int farOffset = _instanceCount;
            int shadowOffset = _instanceCount + (useFarPass ? _instanceCount : 0);
            int visibleInstanceCount = CalculateVegetationVisibleInstanceCapacity(
                _instanceCount,
                useFarPass,
                useShadowPass);
            int drawCommandCapacity = CalculateVegetationDrawCommandCapacity(
                useFarPass,
                useDepthPass,
                useDepthFarPass,
                useShadowPass,
                useMotionPass,
                useMotionFarPass);

            BatchCullingOutputDrawCommands output = HectonBatchRendererGroupUtility.AllocateDirectDrawOutput(
                visibleInstanceCount,
                drawCommandCapacity,
                drawCommandCapacity);

            for (int instanceIndex = 0; instanceIndex < _instanceCount; instanceIndex++)
                output.visibleInstances[nearOffset + instanceIndex] = instanceIndex;

            if (useFarPass)
            {
                for (int instanceIndex = 0; instanceIndex < _instanceCount; instanceIndex++)
                    output.visibleInstances[farOffset + instanceIndex] = instanceIndex;
            }

            if (useShadowPass)
            {
                for (int instanceIndex = 0; instanceIndex < _instanceCount; instanceIndex++)
                    output.visibleInstances[shadowOffset + instanceIndex] = instanceIndex;
            }

            int commandIndex = 0;
            commandIndex = WriteVegetationDrawCommand(
                output,
                commandIndex,
                nearOffset,
                _instanceCount,
                _nearBatchMaterialId,
                _nearBatchMeshId,
                shadowCasting: false,
                receiveShadows: false,
                MotionVectorGenerationMode.Camera);

            if (useFarPass)
            {
                commandIndex = WriteVegetationDrawCommand(
                    output,
                    commandIndex,
                    farOffset,
                    _instanceCount,
                    _farBatchMaterialId,
                    _farBatchMeshId,
                    shadowCasting: false,
                    receiveShadows: false,
                    MotionVectorGenerationMode.Camera);
            }

            if (useDepthPass)
            {
                commandIndex = WriteVegetationDrawCommand(
                    output,
                    commandIndex,
                    nearOffset,
                    _instanceCount,
                    _depthNearBatchMaterialId,
                    _nearBatchMeshId,
                    shadowCasting: false,
                    receiveShadows: false,
                    MotionVectorGenerationMode.Camera);

                if (useDepthFarPass)
                {
                    commandIndex = WriteVegetationDrawCommand(
                        output,
                        commandIndex,
                        farOffset,
                        _instanceCount,
                        _depthFarBatchMaterialId,
                        _farBatchMeshId,
                        shadowCasting: false,
                        receiveShadows: false,
                        MotionVectorGenerationMode.Camera);
                }
            }

            if (useShadowPass)
            {
                commandIndex = WriteVegetationDrawCommand(
                    output,
                    commandIndex,
                    shadowOffset,
                    _instanceCount,
                    _shadowBatchMaterialId,
                    _nearBatchMeshId,
                    shadowCasting: true,
                    receiveShadows: false,
                    MotionVectorGenerationMode.Camera);
            }

            if (useMotionPass)
            {
                commandIndex = WriteVegetationDrawCommand(
                    output,
                    commandIndex,
                    nearOffset,
                    _instanceCount,
                    _motionNearBatchMaterialId,
                    _nearBatchMeshId,
                    shadowCasting: false,
                    receiveShadows: false,
                    MotionVectorGenerationMode.Object);

                if (useMotionFarPass)
                {
                    commandIndex = WriteVegetationDrawCommand(
                        output,
                        commandIndex,
                        farOffset,
                        _instanceCount,
                        _motionFarBatchMaterialId,
                        _farBatchMeshId,
                        shadowCasting: false,
                        receiveShadows: false,
                        MotionVectorGenerationMode.Object);
                }
            }

            output.visibleInstanceCount = visibleInstanceCount;
            output.drawCommandCount = commandIndex;
            output.drawRangeCount = commandIndex;
            FrameTimeWatchdog.ReportBatchRendererGroupBatchCount(commandIndex);
            HectonBatchRendererGroupUtility.WriteDirectDrawOutput(cullingOutput, output);
        }

        private static int CalculateVegetationVisibleInstanceCapacity(
            int instanceCount,
            bool useFarPass,
            bool useShadowPass)
        {
            int visibleInstanceCount = instanceCount;
            if (useFarPass)
                visibleInstanceCount += instanceCount;
            if (useShadowPass)
                visibleInstanceCount += instanceCount;

            return visibleInstanceCount;
        }

        private static int CalculateVegetationDrawCommandCapacity(
            bool useFarPass,
            bool useDepthPass,
            bool useDepthFarPass,
            bool useShadowPass,
            bool useMotionPass,
            bool useMotionFarPass)
        {
            int drawCommandCapacity = 1;
            if (useFarPass)
                drawCommandCapacity++;
            if (useDepthPass)
            {
                drawCommandCapacity++;
                if (useDepthFarPass)
                    drawCommandCapacity++;
            }
            if (useShadowPass)
                drawCommandCapacity++;
            if (useMotionPass)
            {
                drawCommandCapacity++;
                if (useMotionFarPass)
                    drawCommandCapacity++;
            }

            return drawCommandCapacity;
        }

        private unsafe int WriteVegetationDrawCommand(
            BatchCullingOutputDrawCommands output,
            int commandIndex,
            int visibleOffset,
            int visibleCount,
            BatchMaterialID materialId,
            BatchMeshID meshId,
            bool shadowCasting,
            bool receiveShadows,
            MotionVectorGenerationMode motionMode)
        {
            if (visibleCount <= 0 || materialId.value == 0u || meshId.value == 0u)
                return commandIndex;

            output.drawCommands[commandIndex] = new BatchDrawCommand
            {
                flags = BatchDrawCommandFlags.None,
                visibleOffset = (uint)visibleOffset,
                visibleCount = (uint)visibleCount,
                batchID = _batchId,
                materialID = materialId,
                splitVisibilityMask = ushort.MaxValue,
                lightmapIndex = ushort.MaxValue,
                sortingPosition = 0,
                meshID = meshId,
                submeshIndex = (ushort)Mathf.Max(0, _subMeshIndex)
            };
            output.drawRanges[commandIndex] = HectonBatchRendererGroupUtility.CreateDirectDrawRange(
                (uint)commandIndex,
                gameObject.layer,
                shadowCasting ? ShadowCastingMode.On : ShadowCastingMode.Off,
                receiveShadows,
                motionMode);
            return commandIndex + 1;
        }

        private void ReleaseBatchRendererGroupResources()
        {
            if (_batchRendererGroup != null)
            {
                if (_batchId.value != 0u)
                    _batchRendererGroup.RemoveBatch(_batchId);

                UnregisterBatchMesh(ref _nearBatchMeshId);
                UnregisterBatchMesh(ref _farBatchMeshId);
                UnregisterBatchMaterial(ref _nearBatchMaterialId);
                UnregisterBatchMaterial(ref _farBatchMaterialId);
                UnregisterBatchMaterial(ref _depthNearBatchMaterialId);
                UnregisterBatchMaterial(ref _depthFarBatchMaterialId);
                UnregisterBatchMaterial(ref _shadowBatchMaterialId);
                UnregisterBatchMaterial(ref _motionNearBatchMaterialId);
                UnregisterBatchMaterial(ref _motionFarBatchMaterialId);
                _batchRendererGroup.Dispose();
                _batchRendererGroup = null;
            }

            _batchId = default;
            _registeredBatchBuffer = null;
            _registeredNearMesh = null;
            _registeredFarMesh = null;
            _registeredNearBrgMaterial = null;
            _registeredFarBrgMaterial = null;
            _registeredDepthNearBrgMaterial = null;
            _registeredDepthFarBrgMaterial = null;
            _registeredShadowBrgMaterial = null;
            _registeredMotionNearBrgMaterial = null;
            _registeredMotionFarBrgMaterial = null;

            if (_batchHandleBuffer != null)
            {
                _batchHandleBuffer.Release();
                _batchHandleBuffer = null;
            }

            if (_batchMetadata.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_batchMetadata);
                _batchMetadata.Dispose();
            }

            ClearPassMaterialReference(ref _nearBrgMaterial);
            ClearPassMaterialReference(ref _farBrgMaterial);
            ClearPassMaterialReference(ref _depthNearBrgMaterial);
            ClearPassMaterialReference(ref _depthFarBrgMaterial);
            ClearPassMaterialReference(ref _shadowBrgMaterial);
            ClearPassMaterialReference(ref _motionNearBrgMaterial);
            ClearPassMaterialReference(ref _motionFarBrgMaterial);
        }

        private void UnregisterBatchMesh(ref BatchMeshID batchMeshId)
        {
            if (_batchRendererGroup != null && batchMeshId.value != 0u)
                _batchRendererGroup.UnregisterMesh(batchMeshId);

            batchMeshId = default;
        }

        private void UnregisterBatchMaterial(ref BatchMaterialID batchMaterialId)
        {
            if (_batchRendererGroup != null && batchMaterialId.value != 0u)
                _batchRendererGroup.UnregisterMaterial(batchMaterialId);

            batchMaterialId = default;
        }

        private void SyncSourceBinding()
        {
            if (_bufferSource == null)
                return;

            if (_bufferSource is IHectonIndirectVegetationNativeBufferSource nativeBufferSource)
            {
                if (!nativeBufferSource.TryAcquireNativeReadBuffer(out HectonIndirectVegetationNativeReadBuffer readBuffer) ||
                    !HectonIndirectVegetationNativeReadBuffer.IsValid(in readBuffer))
                {
                    ClearBoundInstanceState();
                    if (_bufferSource.HasExplicitBounds)
                        SetDrawBounds(_bufferSource.DrawBounds);
                    else
                        ClearDrawBoundsOverride();
                    return;
                }

                JobHandle producerHandle = readBuffer.ProducerHandle;
                if (!producerHandle.IsCompleted)
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
                    if (HectonIndirectVegetationNativeReadBuffer.HasExplicitBounds(in readBuffer))
                        SetDrawBounds(readBuffer.DrawBounds);
                    else
                        ClearDrawBoundsOverride();
                    return;
                }

                if (HectonIndirectVegetationNativeReadBuffer.HasExplicitBounds(in readBuffer))
                    SetDrawBounds(readBuffer.DrawBounds);
                else
                    ClearDrawBoundsOverride();

                return;
            }

            GraphicsBuffer sourceMatrixBuffer = _bufferSource.InstanceMatrixBuffer;
            GraphicsBuffer sourceDataBuffer = _bufferSource.InstanceDataBuffer;
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
                InvalidateRenderStateForBufferIdentityChange(sourceMatrixBuffer, _instanceDataBuffer, _floraPhaseSeedBuffer);
                _instanceMatrixBuffer = sourceMatrixBuffer;
                _hasCpuCullingData = false;
                _floraAgesAuthoredExternally = false;
            }

            if (_instanceDataBuffer != sourceDataBuffer)
            {
                InvalidateRenderStateForBufferIdentityChange(_instanceMatrixBuffer, sourceDataBuffer != null && sourceDataBuffer.count > 0 ? sourceDataBuffer : null, _floraPhaseSeedBuffer);
                _instanceDataBuffer = sourceDataBuffer != null && sourceDataBuffer.count > 0 ? sourceDataBuffer : null;
                _hasCpuCullingData = false;
                _floraAgesAuthoredExternally = false;
            }

            SetInstanceCount(sourceInstanceCount);

            if (_bufferSource.HasExplicitBounds)
                SetDrawBounds(_bufferSource.DrawBounds);
            else
                ClearDrawBoundsOverride();
        }

        private void ClearBoundInstanceState()
        {
            InvalidateRenderStateForBufferIdentityChange(null, null, null);
            _instanceMatrixBuffer = null;
            _instanceDataBuffer = null;
            _floraPhaseSeedBuffer = null;
            _instanceCount = 0;
            _legacyDataDirty = true;
            _floraAgeBufferDirty = true;
            _floraAgesAuthoredExternally = false;
            _hasCpuCullingData = false;
        }

        private void InvalidateRenderStateForBufferIdentityChange(
            GraphicsBuffer nextMatrixBuffer,
            GraphicsBuffer nextDataBuffer,
            GraphicsBuffer nextPhaseSeedBuffer)
        {
            if (_instanceMatrixBuffer == nextMatrixBuffer &&
                _instanceDataBuffer == nextDataBuffer &&
                _floraPhaseSeedBuffer == nextPhaseSeedBuffer)
            {
                return;
            }

            bool hadActiveBinding = _instanceMatrixBuffer != null ||
                                    _instanceDataBuffer != null ||
                                    _floraPhaseSeedBuffer != null ||
                                    _batchRendererGroup != null;
            if (!hadActiveBinding)
                return;

            _floraAgeBufferDirty = true;
            _hasPreviousMotionCameraPosition = false;
            _previousMotionCamera = null;
            ReleaseBatchRendererGroupResources();
            ReleaseGpuIndirectResources();
        }

        private int CopyScooterHeadlightPayload()
        {
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

            IPlayerRuntimeContext playerContext = Hecton8.Core.GlobalRegistry.Player;
            if (playerContext != null && playerContext.ToolManager != null)
            {
                _playerToolManager = playerContext.ToolManager;
                return;
            }

            playerTransform.TryGetComponent(out _playerToolManager);
        }

        private static Vector4 ResolveVegetationFloatingOffset()
        {
            Vector3 totalOffset = HectonMapMagicVegetationBridge.GlobalTotalUniverseOffset;
            return new Vector4(totalOffset.x, totalOffset.y, totalOffset.z, 0f);
        }

        private GraphicsBuffer ResolveActiveInstanceDataBuffer()
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
                GraphicsBufferUploadUtility.UploadArray(_legacyInstanceDataBuffer, _legacyInstanceData, _instanceCount);
                _legacyDataDirty = false;
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

            if (_legacyInstanceDataBuffer != null && _instanceDataBuffer == null && _instanceCount > 0)
                InvalidateRenderStateForBufferIdentityChange(_instanceMatrixBuffer, null, _floraPhaseSeedBuffer);

            ReleaseLegacyInstanceDataBuffer();

            // COLD ALLOC: HectonVegetationInstanceData[nextCapacity] - legacy metadata fallback staging - owner: HectonIndirectVegetationRenderer
            _legacyInstanceData = new HectonVegetationInstanceData[nextCapacity];
            // COLD ALLOC: GraphicsBuffer[nextCapacity] - legacy instance metadata fallback buffer - owner: HectonIndirectVegetationRenderer
            _legacyInstanceDataBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<HectonVegetationInstanceData>(nextCapacity);
            _legacyDataDirty = true;
        }

        private void EnsureUploadedInstanceBufferCapacity(int instanceCount, bool requiresInstanceDataBuffer)
        {
            if (instanceCount <= 0)
                return;

            if (_uploadedInstanceMatrixBuffer == null || _uploadedInstanceMatrixBuffer.count < instanceCount)
            {
                if (_uploadedInstanceMatrixBuffer != null && _instanceMatrixBuffer == _uploadedInstanceMatrixBuffer)
                    InvalidateRenderStateForBufferIdentityChange(null, _instanceDataBuffer == _uploadedInstanceDataBuffer ? null : _instanceDataBuffer, _floraPhaseSeedBuffer);

                if (_uploadedInstanceMatrixBuffer != null)
                {
                    _uploadedInstanceMatrixBuffer.Release();
                    _uploadedInstanceMatrixBuffer = null;
                }

                int nextCapacity = Mathf.NextPowerOfTwo(Mathf.Max(16, instanceCount));
                // COLD ALLOC: GraphicsBuffer[nextCapacity] - owned matrix upload staging buffer - owner: HectonIndirectVegetationRenderer
                _uploadedInstanceMatrixBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<Matrix4x4>(nextCapacity);
            }

            if (!requiresInstanceDataBuffer)
                return;

            if (_uploadedInstanceDataBuffer == null || _uploadedInstanceDataBuffer.count < instanceCount)
            {
                if (_uploadedInstanceDataBuffer != null && _instanceDataBuffer == _uploadedInstanceDataBuffer)
                    InvalidateRenderStateForBufferIdentityChange(_instanceMatrixBuffer == _uploadedInstanceMatrixBuffer ? null : _instanceMatrixBuffer, null, _floraPhaseSeedBuffer);

                if (_uploadedInstanceDataBuffer != null)
                {
                    _uploadedInstanceDataBuffer.Release();
                    _uploadedInstanceDataBuffer = null;
                }

                int nextCapacity = Mathf.NextPowerOfTwo(Mathf.Max(16, instanceCount));
                // COLD ALLOC: GraphicsBuffer[nextCapacity] - owned metadata upload staging buffer - owner: HectonIndirectVegetationRenderer
                _uploadedInstanceDataBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<HectonVegetationInstanceData>(nextCapacity);
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
                    return new HectonVegetationInstanceData(HectonVegetationInstanceType.GiantKelp, 0.55f, 0.8f, 0.5f, -1f, HectonVegetationInstanceData.RuntimeStateIdle, 0f, 0.55f, new Vector4(0.11f, 0.52f, 0.47f, 0.42f), 0.62f, 1.18f, 1f, 0f);
                case HectonVegetationInstanceType.Sargassum:
                    return new HectonVegetationInstanceData(HectonVegetationInstanceType.Sargassum, 0.4f, 0.9f, 0.5f, -1f, HectonVegetationInstanceData.RuntimeStateIdle, 0f, 0.45f, new Vector4(0.08f, 0.42f, 0.38f, 0.26f), 0.78f, 0.94f, 1f, 0f);
                default:
                    return new HectonVegetationInstanceData(HectonVegetationInstanceType.Grass, 0.55f, 1f, 0.5f, -1f, HectonVegetationInstanceData.RuntimeStateIdle, 0f, 0.35f, new Vector4(0.10f, 0.48f, 0.34f, 0.22f), 1.35f, 0.72f, 1f, 0f);
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
            if (_material == null)
                _material = AssetDatabase.LoadAssetAtPath<Material>(VegetationMaterialAssetPath);

            if (_vegetationShader == null)
                _vegetationShader = AssetDatabase.LoadAssetAtPath<Shader>(VegetationShaderAssetPath);

            if (_cullingCompute == null)
                _cullingCompute = AssetDatabase.LoadAssetAtPath<ComputeShader>(ComputeShaderAssetPath);

            if (_abyssalFlowFieldCompute == null)
                _abyssalFlowFieldCompute = AssetDatabase.LoadAssetAtPath<ComputeShader>(AbyssalFlowFieldComputeAssetPath);

            if (_depthPyramidCompute == null)
                _depthPyramidCompute = AssetDatabase.LoadAssetAtPath<ComputeShader>(DepthPyramidComputeAssetPath);

            if (_depthOnlyShader == null)
                _depthOnlyShader = AssetDatabase.LoadAssetAtPath<Shader>(DepthShaderAssetPath);

            if (_shadowCasterShader == null)
                _shadowCasterShader = AssetDatabase.LoadAssetAtPath<Shader>(ShadowShaderAssetPath);

            if (_motionVectorShader == null)
                _motionVectorShader = AssetDatabase.LoadAssetAtPath<Shader>(MotionShaderAssetPath);

            if (_depthOnlyMaterial == null)
                _depthOnlyMaterial = AssetDatabase.LoadAssetAtPath<Material>(DepthMaterialAssetPath);

            if (_shadowCasterMaterial == null)
                _shadowCasterMaterial = AssetDatabase.LoadAssetAtPath<Material>(ShadowMaterialAssetPath);

            if (_motionVectorMaterial == null)
                _motionVectorMaterial = AssetDatabase.LoadAssetAtPath<Material>(MotionMaterialAssetPath);

            _cullFloraKernel = _cullingCompute != null ? _cullingCompute.FindKernel("CullFloraInstances") : -1;
            _cullFloraShadowKernel = _cullingCompute != null ? _cullingCompute.FindKernel("CullFloraShadowInstances") : -1;
            _clearIndirectArgsKernel = _cullingCompute != null ? _cullingCompute.FindKernel("ClearIndirectArgs") : -1;
            _clearFloraSnapFlagsKernel = _abyssalFlowFieldCompute != null ? _abyssalFlowFieldCompute.FindKernel("ClearFloraSnapFlags") : -1;
            _flagSnappedFloraKernel = _abyssalFlowFieldCompute != null ? _abyssalFlowFieldCompute.FindKernel("FlagSnappedFlora") : -1;
            _depthPyramidCopyKernel = _depthPyramidCompute != null ? _depthPyramidCompute.FindKernel("CopyDepthPyramidMip0") : -1;
            _depthPyramidDownsampleKernel = _depthPyramidCompute != null ? _depthPyramidCompute.FindKernel("DownsampleDepthPyramidMip") : -1;
        }
#endif

        private void CreateAuxiliaryMaterials()
        {
            EnsureIndirectPropertyBlocks();
        }

        private void ReleaseAuxiliaryMaterials()
        {
            _nearMaterialBindingState = default;
            _farMaterialBindingState = default;
            _depthNearMaterialBindingState = default;
            _depthFarMaterialBindingState = default;
            _shadowMaterialBindingState = default;
            _motionNearMaterialBindingState = default;
            _motionFarMaterialBindingState = default;
        }

        private static bool HasMainDirectionalShadowLight()
        {
            Light sun = RenderSettings.sun;
            return sun != null && sun.enabled && sun.type == LightType.Directional && sun.shadows != LightShadows.None;
        }

        private static long EstimateGraphicsBufferBytes(GraphicsBuffer buffer)
        {
            return buffer != null ? (long)buffer.count * buffer.stride : 0L;
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

            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTangents(tangents);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.bounds = new Bounds(new Vector3(0f, 0.5f, 0f), new Vector3(1f, 1f, 0.01f));
            return mesh;
        }

        private void TryRegister()
        {
            if (_isRegistered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
            _isRegistered = GlobalRegistry.Updatables.Contains(this);
        }

        private void TryUnregister()
        {
            if (!_isRegistered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            _isRegistered = false;
        }

        private void TryRegisterOriginShiftListener()
        {
            if (_originShiftRegistered)
                return;

            HectonFloatingOrigin.RegisterListener(this);
            _originShiftRegistered = HectonFloatingOrigin.IsListenerRegistered(this);
        }

        private void TryUnregisterOriginShiftListener()
        {
            if (!_originShiftRegistered)
                return;

            HectonFloatingOrigin.UnregisterListener(this);
            _originShiftRegistered = false;
        }
    }
}
