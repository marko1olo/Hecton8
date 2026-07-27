using System;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Gameplay;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;
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
    public class HectonIndirectVegetationRenderer : MonoBehaviour, ILateFrameTickable, ISlowTickable, IOriginShiftListener, IGlobalRegistryHotSwapListener
    {
        /// <summary>Stride of one Matrix4x4 entry expected in the external instance matrix buffer.</summary>
        public const int InstanceMatrixStride = 64;

        /// <summary>Stride of one <see cref="HectonVegetationInstanceData"/> entry expected in the instance metadata buffer.</summary>
        public const int InstanceDataStride = HectonVegetationInstanceData.Stride;

        private const int IndirectArgsCount = 5;
        private const int VisibleIndexStride = sizeof(uint);
        private const int ThreadsPerGroup = 64;
        private const uint PortableMaxComputeThreadsPerGroup = 256u;
        private const int FrustumPlaneCount = 6;
        private const int CpuCullingScratchPlaneCapacity = 16;
        private const int BrgMetadataPlaceholderCount = 1;
        private const int MaxVegetationVisibilityPasses = 3;
        private const int MaxVegetationDrawCommands = 7;
        private const float LodTransitionRangeMeters = 2f;
        private const byte VisibilityMaskNear = 1 << 0;
        private const byte VisibilityMaskFar = 1 << 1;
        private const byte VisibilityMaskShadow = 1 << 2;
        private const int FloraGrowthTelemetryFrameCount = 300;
        private const int FloraGrowthTelemetryMaxSamples = 64;
        private const uint FloraGrowthTelemetryHashSeed = 2166136261u;
        private const int ScatterCullTelemetryFrameCount = 300;
        private const int ScatterCullTelemetryCounterCount = 4;
        private const int ScatterCullTelemetryReadbackStrideFrames = 30;
        private const int ScatterCullTelemetryTotalCounter = 0;
        private const int ScatterCullTelemetryFrustumCounter = 1;
        private const int ScatterCullTelemetryOcclusionCounter = 2;
        private const int ScatterCullTelemetryVisibleCounter = 3;
        private const int ScatterCullOverdrawWarningVisibleCount = 50000;
        private const SystemID VaultOwnerSystemId = SystemID.GraphicsScalability;
        private const BufferID FloraAgeBufferId = BufferID.HectonIndirectVegetationRenderer_FloraAgeBufferId;
        private const BufferID CpuCullingMatricesBufferId = BufferID.HectonIndirectVegetationRenderer_CpuCullingMatricesBufferId;
        private const BufferID CpuCullingDataBufferId = BufferID.HectonIndirectVegetationRenderer_CpuCullingDataBufferId;
        private const BufferID NativeUploadMatrixDirtyPagesAId = BufferID.HectonIndirectVegetationRenderer_NativeUploadMatrixDirtyPagesAId;
        private const BufferID NativeUploadMatrixDirtyPagesBId = BufferID.HectonIndirectVegetationRenderer_NativeUploadMatrixDirtyPagesBId;
        private const BufferID NativeUploadDataDirtyPagesAId = BufferID.HectonIndirectVegetationRenderer_NativeUploadDataDirtyPagesAId;
        private const BufferID NativeUploadDataDirtyPagesBId = BufferID.HectonIndirectVegetationRenderer_NativeUploadDataDirtyPagesBId;
        private const BufferID FloraGrowthTelemetryBufferId = BufferID.IndirectVegetationFloraGrowthTelemetryRing;
        private const BufferID ScatterCullTelemetryBufferId = BufferID.IndirectVegetationScatterCullTelemetryRing;
        private const int NativeUploadDirtyPageSize = 256;
        private const int NativeUploadMinimumBudgetBytes = 32 * 1024;
        private const int NativeUploadMaximumBudgetBytes = 2 * 1024 * 1024;
        private const int MockScatterDefaultAxisCount = 100;
        private const float MockScatterDefaultSpacing = 2f;
        private const uint MockScatterDefaultSeed = 0x53484939u;
        private const Allocator TransientVegetationCullingAllocator = Allocator.TempJob;
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
        private static readonly int _H8GlobalQualityWeightId = Shader.PropertyToID("_H8GlobalQualityWeight");
        private static readonly int _SourceInstanceCountId = Shader.PropertyToID("_HectonSourceInstanceCount");
        private static readonly int _ViewProjectionId = Shader.PropertyToID("_HectonViewProjection");
        private static readonly int _ViewMatrixId = Shader.PropertyToID("_HectonViewMatrix");
        private static readonly int _CameraPositionId = Shader.PropertyToID("_HectonCameraPosition");

        // The vertex animation needs a camera position that does NOT change per pass. _HectonCameraPosition
        // above only ever reaches the culling compute, and _WorldSpaceCameraPos is the LIGHT during a
        // shadow pass - using it there would make the shadow bend flora at a different distance than
        // ForwardLit does. w is 1 to mark the value as written; the shaders fall back when it is not.
        private static readonly int _VegetationViewPositionId = Shader.PropertyToID("_HectonVegetationViewPositionWS");

        // Authored on the lit material only (they live in its UnityPerMaterial CBUFFER). Pushed into
        // every property block so the depth/shadow/motion passes bend identically without four
        // materials having to be kept in sync by hand.
        private static readonly int _InteractionPushStrengthId = Shader.PropertyToID("_InteractionPushStrength");
        private static readonly int _InteractionVelocityBiasId = Shader.PropertyToID("_InteractionVelocityBias");
        private static readonly int _InteractionDistancePowerId = Shader.PropertyToID("_InteractionDistancePower");
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
        private static readonly int _DensityKeepProbabilityId = Shader.PropertyToID("_HectonDensityKeepProbability01");
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
        [FormerlySerializedAs("_mesh")]
        [Tooltip("Authored near mesh baked offline by FloraTopologyStudio1711. Player runtime never creates this geometry.")]
        private Mesh _authoredNearMesh;

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

#if UNITY_EDITOR
        [Header("Editor Mesh Authoring")]
        [SerializeField]
        [FormerlySerializedAs("_generateMeshAtRuntime")]
        [Tooltip("Editor-only authoring escape hatch. Player runtime ignores procedural mesh construction.")]
        private bool _generateMeshInEditor = false;

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
#endif

        [Header("Impostor Cards")]
        [SerializeField]
        [FormerlySerializedAs("_impostorMesh")]
        [Tooltip("Authored far impostor mesh baked offline. If empty, the near authored mesh is reused.")]
        private Mesh _authoredImpostorMesh;

#if UNITY_EDITOR
        [SerializeField]
        [FormerlySerializedAs("_generateImpostorMeshAtRuntime")]
        [Tooltip("Editor-only authoring escape hatch. Player runtime ignores procedural impostor construction.")]
        private bool _generateImpostorMeshInEditor = false;
#endif

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
        private bool _enableCullTelemetry;

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

#if UNITY_EDITOR
        private Mesh _generatedMesh;
        private Mesh _generatedImpostorMesh;
#endif
        private GraphicsBuffer _instanceMatrixBuffer;
        private GraphicsBuffer _instanceDataBuffer;
        private GraphicsBuffer _floraPhaseSeedBuffer;
        private GraphicsBuffer _legacyInstanceDataBuffer;
        private GraphicsBuffer _uploadedInstanceMatrixBuffer;
        private GraphicsBuffer _uploadedInstanceDataBuffer;
        private GraphicsBuffer _uploadedInstanceMatrixBufferA;
        private GraphicsBuffer _uploadedInstanceMatrixBufferB;
        private GraphicsBuffer _uploadedInstanceDataBufferA;
        private GraphicsBuffer _uploadedInstanceDataBufferB;
        private VaultGenerationHandle<byte> _uploadedMatrixDirtyPagesAHandle;
        private VaultGenerationHandle<byte> _uploadedMatrixDirtyPagesBHandle;
        private VaultGenerationHandle<byte> _uploadedDataDirtyPagesAHandle;
        private VaultGenerationHandle<byte> _uploadedDataDirtyPagesBHandle;
        private int _uploadedDirtyPageCapacity;
        private byte[] _uploadedDirtyPageSnapshot;
        private int _uploadedDirtyPageSnapshotCapacity;
        private int _uploadedInstanceWriteBufferIndex;
        private int _lastNativeUploadBufferIndex = int.MinValue;
        private int _lastNativeUploadInstanceCount = -1;
        private int _lastNativeUploadContentRevision = int.MinValue;
        private int _lastNativeDirtySourceBufferIndex = int.MinValue;
        private int _lastNativeDirtySourceInstanceCount = -1;
        private int _lastNativeDirtySourceContentRevision = int.MinValue;
        private long _lastNativeUploadBytes;
        private long _lastNativeUploadAvoidedBytes;
        private IHectonIndirectVegetationBufferSource _bufferSource;
        private Bounds _explicitBounds;
        private bool _hasBoundsOverride;
        private bool _isLateFrameRegistered;
        private bool _isSlowTickRegistered;
        private bool _originShiftRegistered;
        private bool _hotSwapRegistered;
        private bool _legacyDataDirty = true;
        private int _instanceCount;
        private Camera _cachedCullCamera;
        private IVramPressureReadModel _vramPressure;
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
        private IPlayerRuntimeContext _cachedPlayerContext;
        private PlayerToolManager _playerToolManager;
        private BatchRendererGroup _batchRendererGroup;
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
        private bool _indirectPropertyBlocksPrewarmAttempted;
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
        private VaultGenerationHandle<Matrix4x4> _cpuCullingMatricesHandle;
        private VaultGenerationHandle<HectonVegetationInstanceData> _cpuCullingDataHandle;
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
        private GraphicsBuffer _cullTelemetryCountersUploadBuffer;
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
        private int _cullFloraThreadGroupSizeX;
        private int _cullFloraShadowThreadGroupSizeX;
        private int _clearIndirectArgsThreadGroupSizeX;
        private int _clearFloraSnapFlagsThreadGroupSizeX;
        private int _flagSnappedFloraThreadGroupSizeX;
        private int _depthPyramidCopyThreadGroupSizeX;
        private int _depthPyramidCopyThreadGroupSizeY;
        private int _depthPyramidDownsampleThreadGroupSizeX;
        private int _depthPyramidDownsampleThreadGroupSizeY;

        private HectonVegetationInstanceData[] _legacyInstanceData;
        private VaultGenerationHandle<float> _floraAges01Handle;
#if UNITY_EDITOR
        private const int EditorScatterGizmoBoundsCapacity = 96;
        private static readonly Bounds[] s_editorScatterVisibleBounds = new Bounds[EditorScatterGizmoBoundsCapacity]; // COLD ALLOC: Bounds[96] - SHINOBU_09 editor visible flora gizmo cache - owner: HectonIndirectVegetationRenderer
        private static readonly Bounds[] s_editorScatterCulledBounds = new Bounds[EditorScatterGizmoBoundsCapacity]; // COLD ALLOC: Bounds[96] - SHINOBU_09 editor culled flora gizmo cache - owner: HectonIndirectVegetationRenderer
        [SerializeField]
        private bool _drawEditorScatterDebugGizmos;
#endif
        private VaultGenerationHandle<FloraGrowthTelemetryEntry> _floraGrowthTelemetryHandle;
        private VaultGenerationHandle<ScatterCullTelemetryEntry> _scatterCullTelemetryHandle;
        private IDataVault _dataVault;
        private uint[] _cullTelemetryClearPayload;
        private int _floraGrowthTelemetryCursor;
        private int _lastFloraGrowthTelemetryFrame = -1;
        private int _scatterCullTelemetryCursor;
        private int _lastScatterCullTelemetryFrame = -1;
        private int _lastScatterCullTelemetrySampleFrame = -1;
        private int _resolvedDensityDecimationStep = 1;
        private float _cachedQualityWeight01 = 1f;
        private float _cachedSystemStress01;
        private AsyncGPUReadbackRequest _cullTelemetryReadbackRequest;
        private CullTelemetryReadbackOwner _cullTelemetryReadback;
        private bool _floraGrowthTelemetryDumped;
        private bool _scatterCullTelemetryReadbackPending;
        private bool _scatterCullTelemetryReadbackRepairRequested;
        private bool _scatterCullTelemetryReadbackDisposeAfterCompletion;
        private bool _scatterCullTelemetryReleaseCountersBufferAfterCompletion;
        private GraphicsBuffer _scatterCullTelemetryHeldCountersBuffer;
        private Action<AsyncGPUReadbackRequest> _scatterCullTelemetryReadbackCompletion;
        private bool _scatterCullTelemetryDumped;

        private struct CullTelemetryReadbackOwner
        {
            public NativeArray<uint> Data;
        }
        private bool _lastCullOverdrawWarning;
        private bool _supportsComputeShadersCold;
        private bool _usesReversedZBufferCold;
        private Texture _cachedCameraDepthTexture;
        private Vector4 _cachedZBufferParams = new Vector4(0f, 1f, 0f, 1f);
        private Vector4 _cachedSubmarineWashVelocity;
        private Vector4 _cachedSubmarineWashSphere;
        private float _cachedFloorBiolumStrength;
        private float _cachedOceanBiolumStrength;
        private float _cachedBiolumIntensityScalar;
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
            public float GlobalQualityWeight;
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

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit, Size = 64)]
        private struct FloraGrowthTelemetryEntry
        {
            [System.Runtime.InteropServices.FieldOffset(0)]
            public int FrameIndex;
            [System.Runtime.InteropServices.FieldOffset(4)]
            public int InstanceCount;
            [System.Runtime.InteropServices.FieldOffset(8)]
            public int SampleCount;
            [System.Runtime.InteropServices.FieldOffset(12)]
            public int NegativeAgeCount;
            [System.Runtime.InteropServices.FieldOffset(16)]
            public int NanAgeCount;
            [System.Runtime.InteropServices.FieldOffset(20)]
            public int DirtyUpload;
            [System.Runtime.InteropServices.FieldOffset(24)]
            public float MinAge01;
            [System.Runtime.InteropServices.FieldOffset(28)]
            public float MaxAge01;
            [System.Runtime.InteropServices.FieldOffset(32)]
            public uint AgeHash;
            [System.Runtime.InteropServices.FieldOffset(36)]
            public int Reserved0;
            [System.Runtime.InteropServices.FieldOffset(40)]
            private byte _pad0;
            [System.Runtime.InteropServices.FieldOffset(41)]
            private byte _pad1;
            [System.Runtime.InteropServices.FieldOffset(42)]
            private byte _pad2;
            [System.Runtime.InteropServices.FieldOffset(43)]
            private byte _pad3;
            [System.Runtime.InteropServices.FieldOffset(44)]
            private byte _pad4;
            [System.Runtime.InteropServices.FieldOffset(45)]
            private byte _pad5;
            [System.Runtime.InteropServices.FieldOffset(46)]
            private byte _pad6;
            [System.Runtime.InteropServices.FieldOffset(47)]
            private byte _pad7;
            [System.Runtime.InteropServices.FieldOffset(48)]
            private byte _pad8;
            [System.Runtime.InteropServices.FieldOffset(49)]
            private byte _pad9;
            [System.Runtime.InteropServices.FieldOffset(50)]
            private byte _pad10;
            [System.Runtime.InteropServices.FieldOffset(51)]
            private byte _pad11;
            [System.Runtime.InteropServices.FieldOffset(52)]
            private byte _pad12;
            [System.Runtime.InteropServices.FieldOffset(53)]
            private byte _pad13;
            [System.Runtime.InteropServices.FieldOffset(54)]
            private byte _pad14;
            [System.Runtime.InteropServices.FieldOffset(55)]
            private byte _pad15;
            [System.Runtime.InteropServices.FieldOffset(56)]
            private byte _pad16;
            [System.Runtime.InteropServices.FieldOffset(57)]
            private byte _pad17;
            [System.Runtime.InteropServices.FieldOffset(58)]
            private byte _pad18;
            [System.Runtime.InteropServices.FieldOffset(59)]
            private byte _pad19;
            [System.Runtime.InteropServices.FieldOffset(60)]
            private byte _pad20;
            [System.Runtime.InteropServices.FieldOffset(61)]
            private byte _pad21;
            [System.Runtime.InteropServices.FieldOffset(62)]
            private byte _pad22;
            [System.Runtime.InteropServices.FieldOffset(63)]
            private byte _pad23;
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

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit, Size = 64)]
        private struct ScatterCullTelemetryEntry
        {
            [System.Runtime.InteropServices.FieldOffset(0)]
            public int FrameIndex;
            [System.Runtime.InteropServices.FieldOffset(4)]
            public int TotalInstances;
            [System.Runtime.InteropServices.FieldOffset(8)]
            public int FrustumCulledCount;
            [System.Runtime.InteropServices.FieldOffset(12)]
            public int OcclusionCulledCount;
            [System.Runtime.InteropServices.FieldOffset(16)]
            public int VisibleCount;
            [System.Runtime.InteropServices.FieldOffset(20)]
            public int DensityDecimationStep;
            [System.Runtime.InteropServices.FieldOffset(24)]
            public int OverdrawWarning;
            [System.Runtime.InteropServices.FieldOffset(28)]
            public float SystemStress01;
            [System.Runtime.InteropServices.FieldOffset(32)]
            public float MaxDensity01;
            [System.Runtime.InteropServices.FieldOffset(36)]
            public int Reserved0;
            [System.Runtime.InteropServices.FieldOffset(40)]
            private byte _pad0;
            [System.Runtime.InteropServices.FieldOffset(41)]
            private byte _pad1;
            [System.Runtime.InteropServices.FieldOffset(42)]
            private byte _pad2;
            [System.Runtime.InteropServices.FieldOffset(43)]
            private byte _pad3;
            [System.Runtime.InteropServices.FieldOffset(44)]
            private byte _pad4;
            [System.Runtime.InteropServices.FieldOffset(45)]
            private byte _pad5;
            [System.Runtime.InteropServices.FieldOffset(46)]
            private byte _pad6;
            [System.Runtime.InteropServices.FieldOffset(47)]
            private byte _pad7;
            [System.Runtime.InteropServices.FieldOffset(48)]
            private byte _pad8;
            [System.Runtime.InteropServices.FieldOffset(49)]
            private byte _pad9;
            [System.Runtime.InteropServices.FieldOffset(50)]
            private byte _pad10;
            [System.Runtime.InteropServices.FieldOffset(51)]
            private byte _pad11;
            [System.Runtime.InteropServices.FieldOffset(52)]
            private byte _pad12;
            [System.Runtime.InteropServices.FieldOffset(53)]
            private byte _pad13;
            [System.Runtime.InteropServices.FieldOffset(54)]
            private byte _pad14;
            [System.Runtime.InteropServices.FieldOffset(55)]
            private byte _pad15;
            [System.Runtime.InteropServices.FieldOffset(56)]
            private byte _pad16;
            [System.Runtime.InteropServices.FieldOffset(57)]
            private byte _pad17;
            [System.Runtime.InteropServices.FieldOffset(58)]
            private byte _pad18;
            [System.Runtime.InteropServices.FieldOffset(59)]
            private byte _pad19;
            [System.Runtime.InteropServices.FieldOffset(60)]
            private byte _pad20;
            [System.Runtime.InteropServices.FieldOffset(61)]
            private byte _pad21;
            [System.Runtime.InteropServices.FieldOffset(62)]
            private byte _pad22;
            [System.Runtime.InteropServices.FieldOffset(63)]
            private byte _pad23;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private unsafe struct BuildVegetationVisibilitySlotsJob : IJobParallelFor
        {
            [ReadOnly, NoAlias] public NativeArray<Matrix4x4>.ReadOnly Matrices;
            [ReadOnly, NoAlias] public NativeArray<HectonVegetationInstanceData>.ReadOnly InstanceData;
            public FixedList512Bytes<float4> CullingPlanes;
            public FixedList512Bytes<float4> HeadlightPositionsWs;
            public FixedList512Bytes<float4> HeadlightDirectionsWs;
            public FixedList512Bytes<float4> HeadlightColors;
            public FixedList512Bytes<float4> HeadlightConeData;
            [NativeDisableUnsafePtrRestriction] public int* VisibleInstances;
            public int InstanceCount;
            public int FarScratchOffset;
            public int ShadowScratchOffset;
            public int CullingPlaneCount;
            public int HeadlightCount;
            public byte EnableCpuCullingFlag;
            public byte UseFarPassFlag;
            public byte UseShadowPassFlag;
            public byte BypassDarknessCullingFlag;
            public int DensityDecimationStep;
            public float DensityKeepProbability01;
            public float3 ViewPosition;
            public float3 GlobalOffset;
            public float Lod0MaxDistanceSq;
            public float Lod1MinDistanceSq;
            public float Lod1MaxDistanceSq;

            public void Execute(int index)
            {
                if (index >= InstanceCount)
                    return;

                if (!PassesDensityDecimation(index, DensityDecimationStep, DensityKeepProbability01))
                {
                    WriteVisibilitySlots(index, -1, -1, -1);
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
                        WriteVisibilitySlots(index, -1, -1, -1);
                        return;
                    }

                    if (!IsVisibleInDarkness(centerWs))
                    {
                        WriteVisibilitySlots(index, -1, -1, -1);
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

                WriteVisibilitySlots(
                    index,
                    (instanceVisibility & VisibilityMaskNear) != 0 ? index : -1,
                    (instanceVisibility & VisibilityMaskFar) != 0 ? index : -1,
                    (instanceVisibility & VisibilityMaskShadow) != 0 ? index : -1);
            }

            private void WriteVisibilitySlots(int index, int nearValue, int farValue, int shadowValue)
            {
                VisibleInstances[index] = nearValue;
                if (UseFarPassFlag != 0)
                    VisibleInstances[FarScratchOffset + index] = farValue;
                if (UseShadowPassFlag != 0)
                    VisibleInstances[ShadowScratchOffset + index] = shadowValue;
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

            private static bool PassesDensityDecimation(int index, int decimationStep, float keepProbability01)
            {
                if (math.isfinite(keepProbability01) && keepProbability01 > 0f)
                {
                    float keep01 = math.saturate(keepProbability01);
                    if (keep01 >= 0.999f)
                        return true;

                    uint probabilityHash = Hash((uint)index * 747796405u + 2891336453u);
                    float sample01 = (probabilityHash & 0x00FFFFFFu) * (1f / 16777216f);
                    return sample01 < keep01;
                }

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
                MathLodApproximation.ApproxSinCosBhaskara(angle, out float sin, out float cos);

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
            public int InstanceCount;
            public int FarScratchOffset;
            public int ShadowScratchOffset;
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
                    if (VisibleInstances[instanceIndex] >= 0)
                        nearCount++;
                    if (UseFarPassFlag != 0 && VisibleInstances[FarScratchOffset + instanceIndex] >= 0)
                        farCount++;
                    if (UseShadowPassFlag != 0 && VisibleInstances[ShadowScratchOffset + instanceIndex] >= 0)
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
                    int nearValue = VisibleInstances[instanceIndex];
                    if (nearValue >= 0)
                    {
                        VisibleInstances[nearOffset + nearWrite] = nearValue;
                        nearWrite++;
                    }

                    if (UseFarPassFlag != 0)
                    {
                        int farValue = VisibleInstances[FarScratchOffset + instanceIndex];
                        if (farValue >= 0)
                        {
                            VisibleInstances[farOffset + farWrite] = farValue;
                            farWrite++;
                        }
                    }

                    if (UseShadowPassFlag != 0)
                    {
                        int shadowValue = VisibleInstances[ShadowScratchOffset + instanceIndex];
                        if (shadowValue >= 0)
                        {
                            VisibleInstances[shadowOffset + shadowWrite] = shadowValue;
                            shadowWrite++;
                        }
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

        /// <summary>Read-only vault-owned SoA growth lane uploaded as _HectonFloraAges01. Negative entries are harvested/culling sentinels.</summary>
        public NativeArray<float>.ReadOnly FloraAges01
        {
            get
            {
                return TryReadFloraAges(out NativeArray<float>.ReadOnly floraAges) ? floraAges : default;
            }
        }

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
            if (!TryAcquireFloraAgesForWrite(_instanceCount, out IDataVault vault, out NativeArray<float> floraAges) ||
                instanceIndex >= floraAges.Length)
                return false;

            try
            {
                floraAges[instanceIndex] = SanitizeFloraAgeForUpload(age01);
                _floraAgesAuthoredExternally = true;
                _floraAgeBufferDirty = true;
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in _floraAges01Handle, VaultOwnerSystemId);
            }
        }

        /// <summary>
        /// Marks renderer-owned flora age data for upload after an explicit owner-authorized write path.
        /// </summary>
        public void MarkFloraAgesDirty()
        {
            if (!TryReadFloraAges(out NativeArray<float> _))
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
            if (!TryAcquireFloraAgesForWrite(_instanceCount, out IDataVault vault, out NativeArray<float> floraAges) ||
                floraAges.Length < copyCount)
                return false;

            try
            {
                for (int instanceIndex = 0; instanceIndex < copyCount; instanceIndex++)
                    floraAges[instanceIndex] = SanitizeFloraAgeForUpload(ages01[instanceIndex]);

                _floraAgesAuthoredExternally = true;
                _floraAgeBufferDirty = true;
                return copyCount > 0;
            }
            finally
            {
                vault.ReleaseWriteLock(in _floraAges01Handle, VaultOwnerSystemId);
            }
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
        public bool UsesOwnedUploadBuffers => IsUploadedMatrixBuffer(_instanceMatrixBuffer);

        /// <summary>Approximate VRAM footprint in bytes for the renderer-owned graphics buffers.</summary>
        public long GetVRAMEstimation()
        {
            long totalBytes = 0L;
            totalBytes += EstimateGraphicsBufferBytes(_legacyInstanceDataBuffer);
            totalBytes += EstimateGraphicsBufferBytes(_uploadedInstanceMatrixBufferA);
            totalBytes += EstimateGraphicsBufferBytes(_uploadedInstanceMatrixBufferB);
            totalBytes += EstimateGraphicsBufferBytes(_uploadedInstanceDataBufferA);
            totalBytes += EstimateGraphicsBufferBytes(_uploadedInstanceDataBufferB);
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
            if (!TryReadScatterCullTelemetry(out NativeArray<ScatterCullTelemetryEntry>.ReadOnly scatterCullTelemetry))
                return false;

            int readIndex = _scatterCullTelemetryCursor - 1;
            if (readIndex < 0)
                readIndex = ScatterCullTelemetryFrameCount - 1;

            ScatterCullTelemetryEntry entry = scatterCullTelemetry[readIndex];
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
        /// Builds deterministic no-producer scatter matrices and metadata into transient native arrays, then binds them.
        /// </summary>
        public bool GenerateMockScatterForDiagnostics(int cellsX, int cellsZ, float spacing, uint seed)
        {
            int safeCellsX = Mathf.Clamp(cellsX, 1, 512);
            int safeCellsZ = Mathf.Clamp(cellsZ, 1, 512);
            int count = Mathf.Min(150000, safeCellsX * safeCellsZ);
            if (count <= 0)
                return false;

            _bufferSource = null;
            NativeArray<Matrix4x4> matrices = H8Memory.Allocate<Matrix4x4>(
                count,
                VaultOwnerSystemId,
                TransientVegetationCullingAllocator,
                NativeArrayOptions.UninitializedMemory);
            NativeArray<HectonVegetationInstanceData> instanceData = H8Memory.Allocate<HectonVegetationInstanceData>(
                count,
                VaultOwnerSystemId,
                TransientVegetationCullingAllocator,
                NativeArrayOptions.UninitializedMemory);
            if (!matrices.IsCreated || !instanceData.IsCreated)
            {
                if (matrices.IsCreated)
                    H8Memory.Release(ref matrices, VaultOwnerSystemId);
                if (instanceData.IsCreated)
                    H8Memory.Release(ref instanceData, VaultOwnerSystemId);
                return false;
            }

            try
            {
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
            finally
            {
                if (matrices.IsCreated)
                    H8Memory.Release(ref matrices, VaultOwnerSystemId);
                if (instanceData.IsCreated)
                    H8Memory.Release(ref instanceData, VaultOwnerSystemId);
            }
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
                _instanceCount <= 0 ||
                !TryReadCpuCullingData(_instanceCount, out NativeArray<Matrix4x4>.ReadOnly cpuCullingMatrices, out NativeArray<HectonVegetationInstanceData>.ReadOnly cpuCullingData))
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
                Matrix4x4 matrix = cpuCullingMatrices[instanceIndex];
                HectonVegetationInstanceData data = cpuCullingData[instanceIndex];
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
            _cachedQualityWeight01 = ResolveVegetationQualityWeight01(_cachedQualityWeight01);
            _cachedSystemStress01 = 0f;
            _resolvedDensityDecimationStep = ResolveDensityDecimationStep();
            TryAutoAssignAssets();
            CacheGraphicsCapabilitiesCold();
            if (_cullingCompute != null)
            {
                _cullFloraKernel = ResolveKernel(_cullingCompute, "CullFloraInstances");
                _cullFloraShadowKernel = ResolveKernel(_cullingCompute, "CullFloraShadowInstances");
                _clearIndirectArgsKernel = ResolveKernel(_cullingCompute, "ClearIndirectArgs");
                _cullFloraThreadGroupSizeX = ResolveKernelThreadGroupSizeX(_cullingCompute, _cullFloraKernel);
                _cullFloraShadowThreadGroupSizeX = ResolveKernelThreadGroupSizeX(_cullingCompute, _cullFloraShadowKernel);
                _clearIndirectArgsThreadGroupSizeX = ResolveKernelThreadGroupSizeX(_cullingCompute, _clearIndirectArgsKernel);
            }
            if (_abyssalFlowFieldCompute != null)
            {
                _clearFloraSnapFlagsKernel = ResolveKernel(_abyssalFlowFieldCompute, "ClearFloraSnapFlags");
                _flagSnappedFloraKernel = ResolveKernel(_abyssalFlowFieldCompute, "FlagSnappedFlora");
                _clearFloraSnapFlagsThreadGroupSizeX = ResolveKernelThreadGroupSizeX(_abyssalFlowFieldCompute, _clearFloraSnapFlagsKernel);
                _flagSnappedFloraThreadGroupSizeX = ResolveKernelThreadGroupSizeX(_abyssalFlowFieldCompute, _flagSnappedFloraKernel);
            }
            if (_depthPyramidCompute != null)
            {
                _depthPyramidCopyKernel = ResolveKernel(_depthPyramidCompute, "CopyDepthPyramidMip0");
                _depthPyramidDownsampleKernel = ResolveKernel(_depthPyramidCompute, "DownsampleDepthPyramidMip");
                ResolveKernelThreadGroupSizes(
                    _depthPyramidCompute,
                    _depthPyramidCopyKernel,
                    out _depthPyramidCopyThreadGroupSizeX,
                    out _depthPyramidCopyThreadGroupSizeY);
                ResolveKernelThreadGroupSizes(
                    _depthPyramidCompute,
                    _depthPyramidDownsampleKernel,
                    out _depthPyramidDownsampleThreadGroupSizeX,
                    out _depthPyramidDownsampleThreadGroupSizeY);
            }

            if (!EnsureRenderMaterialResolved())
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogError("[HectonIndirectVegetationRenderer] Material is required and fallback shader resolution failed.", this);
#endif
                enabled = false;
                return;
            }

#if UNITY_EDITOR
            if (_generateMeshInEditor || _authoredNearMesh == null)
            {
                _generatedMesh = HectonProceduralVegetationStripBuilder.Build(
                    "HectonIndirectVegetationRenderer_Strip",
                    _segmentCount,
                    _stripHeight,
                    _stripBaseWidth,
                    _stripTipWidth);
            }

            if ((_generateImpostorMeshInEditor || _authoredImpostorMesh == null) && _farLodDistance > _nearLodDistance)
                _generatedImpostorMesh = BuildImpostorCardMesh();
#endif

            if (ResolveNearRenderMesh() == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogError("[HectonIndirectVegetationRenderer] No near render mesh resolved.", this);
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
            CreateAuxiliaryMaterials();
            RefreshCullCameraCacheCold();
        }

        private void OnEnable()
        {
            CacheGraphicsCapabilitiesCold();
            CachePlayerContextCold();
            CacheRuntimeServicesCold();
            RefreshCullCameraCacheCold();
            TryRegister();
            TryRegisterHotSwapListener();
            TryRegisterOriginShiftListener();
        }

        private void OnDisable()
        {
            TryUnregisterOriginShiftListener();
            TryUnregisterHotSwapListener();
            TryUnregister();
            _hasPreviousMotionCameraPosition = false;
            _previousMotionCamera = null;
            _vramPressure = null;
            ReleaseBatchRendererGroupResources();
            ReleaseGpuIndirectResources();
        }

        private void OnDestroy()
        {
            TryUnregisterOriginShiftListener();
            TryUnregisterHotSwapListener();
            TryUnregister();
            ReleaseBatchRendererGroupResources();
            ReleaseGpuIndirectResources();
            ReleaseLegacyInstanceDataBuffer();
            ReleaseUploadedInstanceBuffers();
            ReleaseFloraAgeResources();
            ReleaseFloraGrowthTelemetryResources();
            ReleaseScatterCullTelemetryResources();
            ReleaseAuxiliaryMaterials();
            ReleaseCpuCullingData();

#if UNITY_EDITOR
            ReleaseEditorGeneratedMeshes();
#endif
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                TryUnregister();
                if (currentService == null || !isActiveAndEnabled)
                    return;

                TryRegister();
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.VRAMPressureRuntime)
            {
                _vramPressure = currentService as IVramPressureReadModel;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                IDataVault previousVault = _dataVault ?? previousService as IDataVault;
                ReleaseFloraGrowthTelemetryResources(previousVault);
                ReleaseScatterCullTelemetryResources(previousVault);
                _dataVault = currentService as IDataVault;
                return;
            }

            if (serviceSlot != GlobalRegistryServiceSlot.Player)
                return;

            _cachedPlayerContext = currentService as IPlayerRuntimeContext;
            _playerToolManager = _cachedPlayerContext != null ? _cachedPlayerContext.ToolManager : null;
        }

        /// <summary>
        /// Binds an external source that owns both instance buffers and optional explicit bounds.
        /// </summary>
        /// <param name="bufferSource">External source that owns the GPU buffers.</param>
        public void BindSource(IHectonIndirectVegetationBufferSource bufferSource)
        {
            if (!ReferenceEquals(_bufferSource, bufferSource))
                InvalidateNativeUploadCache();

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
            InvalidateNativeUploadCache();
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
            InvalidateNativeUploadCache();
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
            GraphicsBuffer matrixWriteBuffer = ResolveUploadedMatrixWriteBuffer();
            GraphicsBuffer matrixMirrorBuffer = ResolveUploadedMatrixMirrorBuffer();
            if (matrixWriteBuffer == null || matrixMirrorBuffer == null)
            {
                ClearInstanceBuffer();
                return;
            }

            GraphicsBufferUploadUtility.UploadArray(matrixWriteBuffer, instanceMatrices, instanceCount);
            if (matrixMirrorBuffer != matrixWriteBuffer)
                GraphicsBufferUploadUtility.UploadArray(matrixMirrorBuffer, instanceMatrices, instanceCount);
            CopyCpuCullingPayload(instanceMatrices, instanceData, instanceCount);

            if (instanceData != null)
            {
                GraphicsBuffer dataWriteBuffer = ResolveUploadedDataWriteBuffer();
                GraphicsBuffer dataMirrorBuffer = ResolveUploadedDataMirrorBuffer();
                if (instanceData.Length < instanceCount || dataWriteBuffer == null || dataMirrorBuffer == null)
                {
                    ClearInstanceBuffer();
                    return;
                }

                GraphicsBufferUploadUtility.UploadArray(dataWriteBuffer, instanceData, instanceCount);
                if (dataMirrorBuffer != dataWriteBuffer)
                    GraphicsBufferUploadUtility.UploadArray(dataMirrorBuffer, instanceData, instanceCount);
                _uploadedInstanceDataBuffer = dataWriteBuffer;
                InvalidateRenderStateForBufferIdentityChange(matrixWriteBuffer, dataWriteBuffer, _floraPhaseSeedBuffer);
                _uploadedInstanceMatrixBuffer = matrixWriteBuffer;
                _instanceMatrixBuffer = matrixWriteBuffer;
                _instanceDataBuffer = dataWriteBuffer;
                _legacyDataDirty = false;
            }
            else
            {
                InvalidateRenderStateForBufferIdentityChange(matrixWriteBuffer, null, _floraPhaseSeedBuffer);
                _uploadedInstanceMatrixBuffer = matrixWriteBuffer;
                _instanceMatrixBuffer = matrixWriteBuffer;
                _instanceDataBuffer = null;
                _legacyDataDirty = true;
            }

            SetInstanceCount(instanceCount);
            ClearUploadedDirtyPages(instanceCount);
            AdvanceUploadedWriteBuffer();
            InvalidateNativeUploadCache();
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
            GraphicsBuffer matrixWriteBuffer = ResolveUploadedMatrixWriteBuffer();
            GraphicsBuffer matrixMirrorBuffer = ResolveUploadedMatrixMirrorBuffer();
            GraphicsBuffer dataWriteBuffer = ResolveUploadedDataWriteBuffer();
            GraphicsBuffer dataMirrorBuffer = ResolveUploadedDataMirrorBuffer();
            if (matrixWriteBuffer == null || matrixMirrorBuffer == null || dataWriteBuffer == null || dataMirrorBuffer == null)
                return false;

            InvalidateRenderStateForBufferIdentityChange(matrixWriteBuffer, dataWriteBuffer, _floraPhaseSeedBuffer);
            GraphicsBufferUploadUtility.UploadNativeArray(matrixWriteBuffer, instanceMatrices, instanceCount);
            if (matrixMirrorBuffer != matrixWriteBuffer)
                GraphicsBufferUploadUtility.UploadNativeArray(matrixMirrorBuffer, instanceMatrices, instanceCount);
            GraphicsBufferUploadUtility.UploadNativeArray(dataWriteBuffer, instanceData, instanceCount);
            if (dataMirrorBuffer != dataWriteBuffer)
                GraphicsBufferUploadUtility.UploadNativeArray(dataMirrorBuffer, instanceData, instanceCount);
            _uploadedInstanceMatrixBuffer = matrixWriteBuffer;
            _uploadedInstanceDataBuffer = dataWriteBuffer;
            _instanceMatrixBuffer = matrixWriteBuffer;
            _instanceDataBuffer = dataWriteBuffer;
            _legacyDataDirty = false;
            CopyCpuCullingPayload(instanceMatrices, instanceData, instanceCount);
            SetInstanceCount(instanceCount);
            ClearUploadedDirtyPages(instanceCount);
            AdvanceUploadedWriteBuffer();
            return true;
        }

        private bool BindInstanceNativeReadBuffer(in HectonIndirectVegetationNativeReadBuffer readBuffer)
        {
            bool hasDirtyPageContract = HectonIndirectVegetationNativeReadBuffer.HasDirtyPages(in readBuffer);
            if (!hasDirtyPageContract || !CanUseDirtyPageUpload(in readBuffer))
            {
                bool fullUploadSucceeded = BindInstanceNativeArrays(
                    readBuffer.InstanceMatrices,
                    readBuffer.InstanceData,
                    readBuffer.InstanceCount);
                if (fullUploadSucceeded)
                    RecordNativeUpload(in readBuffer, EstimateNativeUploadBytes(readBuffer.InstanceCount) * 2L);
                return fullUploadSucceeded;
            }

            return BindInstanceNativeDirtyPages(in readBuffer);
        }

        private bool BindInstanceNativeDirtyPages(in HectonIndirectVegetationNativeReadBuffer readBuffer)
        {
            int instanceCount = readBuffer.InstanceCount;
            EnsureUploadedInstanceBufferCapacity(instanceCount, true);
            if (!EnsureUploadedDirtyPageCapacity(instanceCount))
                return false;

            GraphicsBuffer matrixWriteBuffer = ResolveUploadedMatrixWriteBuffer();
            GraphicsBuffer dataWriteBuffer = ResolveUploadedDataWriteBuffer();
            if (matrixWriteBuffer == null || dataWriteBuffer == null)
                return false;

            if (_lastNativeUploadInstanceCount != instanceCount)
            {
                // The repack that changes the aggregate count can shift every surviving
                // instance, so no source page mask is trustworthy across a count change:
                // mark everything dirty once and let the upload budget drain it. The count
                // is recorded immediately so deferred pages are not re-marked next frame.
                if (!TryMarkAllUploadedDirtyPages(instanceCount))
                    return false;

                _lastNativeUploadInstanceCount = instanceCount;
            }

            bool sourceMatrixDirty = GraphicsBufferUploadUtility.HasAnyDirtyPage(readBuffer.MatrixDirtyPages, instanceCount, readBuffer.DirtyPageSize);
            bool sourceDataDirty = GraphicsBufferUploadUtility.HasAnyDirtyPage(readBuffer.InstanceDataDirtyPages, instanceCount, readBuffer.DirtyPageSize);
            if (!sourceMatrixDirty &&
                !sourceDataDirty &&
                readBuffer.ContentRevision != _lastNativeUploadContentRevision &&
                !HasUploadedWriteDirtyPageBacklog(instanceCount))
            {
                bool fullUploadSucceeded = BindInstanceNativeArrays(readBuffer.InstanceMatrices, readBuffer.InstanceData, instanceCount);
                if (fullUploadSucceeded)
                    RecordNativeUpload(in readBuffer, EstimateNativeUploadBytes(instanceCount) * 2L);
                return fullUploadSucceeded;
            }

            if ((sourceMatrixDirty || sourceDataDirty) &&
                !HasAbsorbedNativeSourceDirtyPages(in readBuffer))
            {
                if (sourceMatrixDirty &&
                    (!TryMarkUploadedDirtyPages(readBuffer.MatrixDirtyPages, ref _uploadedMatrixDirtyPagesAHandle, NativeUploadMatrixDirtyPagesAId, instanceCount) ||
                     !TryMarkUploadedDirtyPages(readBuffer.MatrixDirtyPages, ref _uploadedMatrixDirtyPagesBHandle, NativeUploadMatrixDirtyPagesBId, instanceCount)))
                {
                    return false;
                }

                if (sourceDataDirty &&
                    (!TryMarkUploadedDirtyPages(readBuffer.InstanceDataDirtyPages, ref _uploadedDataDirtyPagesAHandle, NativeUploadDataDirtyPagesAId, instanceCount) ||
                     !TryMarkUploadedDirtyPages(readBuffer.InstanceDataDirtyPages, ref _uploadedDataDirtyPagesBHandle, NativeUploadDataDirtyPagesBId, instanceCount)))
                {
                    return false;
                }

                RecordNativeSourceDirtyPagesAbsorbed(in readBuffer);
            }

            int uploadBudgetBytes = ResolveNativeUploadBudgetBytes();
            GraphicsBufferUploadUtility.PageUploadStats matrixStats = default;
            if (!TryUploadMatrixDirtyPages(matrixWriteBuffer, readBuffer.InstanceMatrices, instanceCount, uploadBudgetBytes, out bool matrixDirty, out matrixStats))
                return false;

            long consumedMatrixBudget = matrixStats.UploadedBytes > uploadBudgetBytes ? uploadBudgetBytes : matrixStats.UploadedBytes;
            long remainingBudgetBytes = (long)uploadBudgetBytes - consumedMatrixBudget;
            int dataFirstDirtyPageBytes;
            if (!TryResolveDataDirtyPageUploadState(instanceCount, out bool dataDirty, out dataFirstDirtyPageBytes))
                return false;

            if (!matrixDirty && !dataDirty)
            {
                RecordNativeUpload(in readBuffer, 0L);
                return true;
            }

            bool canUploadDataThisFrame =
                dataDirty &&
                dataFirstDirtyPageBytes > 0 &&
                (remainingBudgetBytes >= dataFirstDirtyPageBytes || matrixStats.UploadedBytes <= 0L);
            long dataBudgetLong = remainingBudgetBytes > 1L ? remainingBudgetBytes : 1L;
            int dataBudgetBytes = dataBudgetLong > int.MaxValue ? int.MaxValue : (int)dataBudgetLong;
            GraphicsBufferUploadUtility.PageUploadStats dataStats = default;
            if (canUploadDataThisFrame &&
                !TryUploadDataDirtyPages(dataWriteBuffer, readBuffer.InstanceData, instanceCount, dataBudgetBytes, out dataStats))
            {
                return false;
            }

            bool dataDeferredByBudget = dataDirty && !canUploadDataThisFrame;
            long uploadedBytes = matrixStats.UploadedBytes + dataStats.UploadedBytes;
            _lastNativeUploadBytes = uploadedBytes;
            long avoidedBytes = EstimateNativeUploadBytes(instanceCount) - uploadedBytes;
            _lastNativeUploadAvoidedBytes = avoidedBytes > 0L ? avoidedBytes : 0L;
            if (matrixStats.DeferredPages > 0 || dataStats.DeferredPages > 0 || dataDeferredByBudget)
                return true;

            InvalidateRenderStateForBufferIdentityChange(matrixWriteBuffer, dataWriteBuffer, _floraPhaseSeedBuffer);
            _uploadedInstanceMatrixBuffer = matrixWriteBuffer;
            _uploadedInstanceDataBuffer = dataWriteBuffer;
            _instanceMatrixBuffer = matrixWriteBuffer;
            _instanceDataBuffer = dataWriteBuffer;
            _legacyDataDirty = false;
            CopyCpuCullingPayload(readBuffer.InstanceMatrices, readBuffer.InstanceData, instanceCount);
            SetInstanceCount(instanceCount);
            AdvanceUploadedWriteBuffer();
            RecordNativeUpload(in readBuffer, uploadedBytes);
            return true;
        }

        private bool CanUseDirtyPageUpload(in HectonIndirectVegetationNativeReadBuffer readBuffer)
        {
            int requiredPages = GraphicsBufferUploadUtility.ResolveDirtyPageCount(readBuffer.InstanceCount, NativeUploadDirtyPageSize);
            // Buffer capacity decides eligibility, not count equality with the last upload:
            // the aggregate instance count changes on almost every finished chunk build, and
            // requiring a bit-exact match pushed exactly those frames onto the unbudgeted
            // four-way full UploadNativeArray path (~20 MB in one frame at ~40k instances).
            // A count change instead re-marks every page (BindInstanceNativeDirtyPages) and
            // the budgeted page uploader spreads the same bytes across frames.
            return readBuffer.InstanceCount > 0 &&
                   readBuffer.DirtyPageSize == NativeUploadDirtyPageSize &&
                   _uploadedInstanceMatrixBufferA != null &&
                   _uploadedInstanceMatrixBufferB != null &&
                   _uploadedInstanceMatrixBufferA.count >= readBuffer.InstanceCount &&
                   _uploadedInstanceMatrixBufferB.count >= readBuffer.InstanceCount &&
                   _uploadedInstanceDataBufferA != null &&
                   _uploadedInstanceDataBufferB != null &&
                   _uploadedInstanceDataBufferA.count >= readBuffer.InstanceCount &&
                   _uploadedInstanceDataBufferB.count >= readBuffer.InstanceCount &&
                   IsUploadedMatrixBuffer(_instanceMatrixBuffer) &&
                   IsUploadedDataBuffer(_instanceDataBuffer) &&
                   HasUploadedDirtyPageStorage(requiredPages);
        }

        private bool CanReuseNativeUpload(in HectonIndirectVegetationNativeReadBuffer readBuffer)
        {
            if (HectonIndirectVegetationNativeReadBuffer.HasDirtyPages(in readBuffer) &&
                (GraphicsBufferUploadUtility.HasAnyDirtyPage(readBuffer.MatrixDirtyPages, readBuffer.InstanceCount, readBuffer.DirtyPageSize) ||
                 GraphicsBufferUploadUtility.HasAnyDirtyPage(readBuffer.InstanceDataDirtyPages, readBuffer.InstanceCount, readBuffer.DirtyPageSize)))
            {
                if (!HasAbsorbedNativeSourceDirtyPages(in readBuffer) ||
                    HasUploadedWriteDirtyPageBacklog(readBuffer.InstanceCount))
                {
                    return false;
                }
            }

            return IsUploadedMatrixBuffer(_instanceMatrixBuffer) &&
                   IsUploadedDataBuffer(_instanceDataBuffer) &&
                   _lastNativeUploadBufferIndex == readBuffer.BufferIndex &&
                   _lastNativeUploadInstanceCount == readBuffer.InstanceCount &&
                   _lastNativeUploadContentRevision == readBuffer.ContentRevision;
        }

        private void RecordNativeUpload(in HectonIndirectVegetationNativeReadBuffer readBuffer, long uploadedBytes)
        {
            _lastNativeUploadBufferIndex = readBuffer.BufferIndex;
            _lastNativeUploadInstanceCount = readBuffer.InstanceCount;
            _lastNativeUploadContentRevision = readBuffer.ContentRevision;
            RecordNativeSourceDirtyPagesAbsorbed(in readBuffer);
            _lastNativeUploadBytes = uploadedBytes > 0L ? uploadedBytes : 0L;
            long avoidedBytes = EstimateNativeUploadBytes(readBuffer.InstanceCount) - _lastNativeUploadBytes;
            _lastNativeUploadAvoidedBytes = avoidedBytes > 0L ? avoidedBytes : 0L;
        }

        private bool HasAbsorbedNativeSourceDirtyPages(in HectonIndirectVegetationNativeReadBuffer readBuffer)
        {
            return _lastNativeDirtySourceBufferIndex == readBuffer.BufferIndex &&
                   _lastNativeDirtySourceInstanceCount == readBuffer.InstanceCount &&
                   _lastNativeDirtySourceContentRevision == readBuffer.ContentRevision;
        }

        private void RecordNativeSourceDirtyPagesAbsorbed(in HectonIndirectVegetationNativeReadBuffer readBuffer)
        {
            _lastNativeDirtySourceBufferIndex = readBuffer.BufferIndex;
            _lastNativeDirtySourceInstanceCount = readBuffer.InstanceCount;
            _lastNativeDirtySourceContentRevision = readBuffer.ContentRevision;
        }

        private void InvalidateNativeUploadCache()
        {
            _lastNativeUploadBufferIndex = int.MinValue;
            _lastNativeUploadInstanceCount = -1;
            _lastNativeUploadContentRevision = int.MinValue;
            _lastNativeDirtySourceBufferIndex = int.MinValue;
            _lastNativeDirtySourceInstanceCount = -1;
            _lastNativeDirtySourceContentRevision = int.MinValue;
            _lastNativeUploadBytes = 0L;
            _lastNativeUploadAvoidedBytes = 0L;
        }

        private static long EstimateNativeUploadBytes(int instanceCount)
        {
            return (long)math.max(0, instanceCount) * (InstanceMatrixStride + InstanceDataStride);
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
            InvalidateNativeUploadCache();
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
            float shiftSqrMagnitude = shiftOffset.sqrMagnitude;
            if (!math.all(math.isfinite(new float3(shiftOffset.x, shiftOffset.y, shiftOffset.z))) ||
                !math.isfinite(shiftSqrMagnitude) ||
                shiftSqrMagnitude <= 0.000001f)
            {
                return;
            }

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
        public void LateFrameTick()
        {
            RunVisualTick();
        }

        public void SlowTick()
        {
            SyncSourceBinding();
            ConsumeScatterRuntimeSignals();

            Camera cullCamera = ResolveCullCamera();
            RefreshExternalShaderGlobalsCold(cullCamera);
            CreateAuxiliaryMaterials();
            PrepareGpuIndirectResourcesCold(cullCamera);
            FlushCullTelemetryReadbackRepairSlow();
        }

        private void RunVisualTick()
        {
            ConsumeScatterRuntimeSignals();
            PollCullTelemetryReadback();

            Material renderMaterial = ResolveRenderMaterial();
            if (_instanceMatrixBuffer == null || _instanceCount <= 0 || renderMaterial == null)
                return;

            Mesh nearMesh = ResolveNearRenderMesh();
            if (nearMesh == null)
                return;

            Camera cullCamera = ResolveCullCamera();
            Vector3 cullCameraPosition = _cachedCullCameraPosition;
            Vector3 cullCameraForward = _cachedCullCameraForward;
            if (cullCamera != null)
            {
                Transform cullTransform = cullCamera.transform;
                ResolveCullCameraPose(cullTransform, out cullCameraPosition, out cullCameraForward);
                _cachedCullCameraPosition = cullCameraPosition;
                _cachedCullCameraForward = cullCameraForward;
            }

            Mesh farMesh = FrameTimeWatchdog.IsDistantFloraRenderingEnabled && _farLodDistance > _nearLodDistance
                ? ResolveImpostorRenderMesh()
                : null;
            Vector3 rendererPosition = ResolveRendererRuntimePosition();
            Bounds drawBounds = ResolveDrawBounds(rendererPosition);
            if (TryRenderGpuIndirect(cullCamera, nearMesh, farMesh, cullCameraPosition, cullCameraForward, drawBounds))
                return;

            ReleaseBatchRendererGroupResources();
        }

        private void PrepareGpuIndirectResourcesCold(Camera cullCamera)
        {
            if (!_preferGpuIndirectRendering ||
                !_supportsComputeShadersCold ||
                _cullingCompute == null ||
                _clearIndirectArgsKernel < 0 ||
                _instanceMatrixBuffer == null ||
                _instanceCount <= 0)
            {
                return;
            }

            Mesh nearMesh = ResolveNearRenderMesh();
            if (nearMesh == null)
                return;

            Mesh farMesh = FrameTimeWatchdog.IsDistantFloraRenderingEnabled && _farLodDistance > _nearLodDistance
                ? ResolveImpostorRenderMesh()
                : null;

            GraphicsBuffer activeInstanceDataBuffer = ResolveActiveInstanceDataBuffer();
            if (activeInstanceDataBuffer == null)
                return;

            EnsureGpuIndirectResources(_instanceCount, nearMesh, farMesh);
            EnsureFloraGrowthTelemetry();
            EnsureScatterCullTelemetry();
            _ = ResolveFloraAgeBuffer();
            EnsureDepthPyramidResourcesForCameraCold(cullCamera);
        }

        private void RefreshExternalShaderGlobalsCold(Camera cullCamera)
        {
            _cachedCameraDepthTexture = Shader.GetGlobalTexture(_GlobalCameraDepthTextureId);
            _cachedZBufferParams = cullCamera != null
                ? ResolveZBufferParams(cullCamera)
                : Shader.GetGlobalVector(_GlobalZBufferParamsId);
            _cachedFloorBiolumStrength = SanitizeNonNegative(Shader.GetGlobalFloat(_FloorBiolumStrengthId));
            _cachedOceanBiolumStrength = SanitizeNonNegative(Shader.GetGlobalFloat(_OceanBiolumStrengthId));
            _cachedBiolumIntensityScalar = ResolveBiolumIntensityScalarCold();
            _cachedSubmarineWashVelocity = Shader.GetGlobalVector(_SubmarineWashVelocityId);
            _cachedSubmarineWashSphere = Shader.GetGlobalVector(_SubmarineWashSphereId);
        }

        private static float SanitizeNonNegative(float value)
        {
            return math.isfinite(value) ? math.max(0f, value) : 0f;
        }

        private float ResolveBiolumIntensityScalarCold()
        {
            Vector4 intensity = Shader.GetGlobalVector(_BiolumIntensityVectorId);
            return SanitizeNonNegative(intensity.x);
        }

        private void ConsumeScatterRuntimeSignals()
        {
            _cachedQualityWeight01 = ResolveVegetationQualityWeight01(_cachedQualityWeight01);

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
            return ResolveDensityDecimationStep(ResolveDensityKeepProbability01());
        }

        private static int ResolveDensityDecimationStep(float densityKeepProbability01)
        {
            float keep01 = math.saturate(math.select(1f, densityKeepProbability01, math.isfinite(densityKeepProbability01)));
            if (keep01 >= 0.999f)
                return 1;

            return Mathf.Clamp(Mathf.CeilToInt(1f / Mathf.Max(keep01, 0.25f)), 1, 4);
        }

        private float ResolveDensityKeepProbability01()
        {
            int step = Mathf.Clamp(_minimumDensityDecimationStep, 1, 4);
            float maxDensity = Mathf.Clamp(_maxDensity01, 0.05f, 1f);
            float minimumStepKeep01 = 1f / step;
            float qualityKeep01 = math.lerp(0.25f, 1f, Smooth01(_cachedQualityWeight01));
            float stressKeep01 = math.lerp(1f, 0.25f, Smooth01(_cachedSystemStress01));
            return math.saturate(math.min(math.min(maxDensity, minimumStepKeep01), math.min(qualityKeep01, stressKeep01)));
        }

        private static float Smooth01(float value)
        {
            float x = math.saturate(value);
            return x * x * (3f - 2f * x);
        }

        private static float ResolveVegetationQualityWeight01(float fallback01)
        {
            float fallback = math.saturate(math.select(1f, fallback01, math.isfinite(fallback01)));
            float qualityWeight = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.select(fallback, qualityWeight, math.isfinite(qualityWeight)));
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

#if UNITY_EDITOR
        private void ReleaseEditorGeneratedMeshes()
        {
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
#endif

        private Mesh ResolveNearRenderMesh()
        {
#if UNITY_EDITOR
            return _generatedMesh != null ? _generatedMesh : _authoredNearMesh;
#else
            return _authoredNearMesh;
#endif
        }

        private Mesh ResolveImpostorRenderMesh()
        {
#if UNITY_EDITOR
            if (_generatedImpostorMesh != null)
                return _generatedImpostorMesh;
#endif

            if (_authoredImpostorMesh != null)
                return _authoredImpostorMesh;

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

            _batchHandleBuffer = HectonBatchRendererGroupUtility.CreateBatchHandleBuffer(); // COLD ALLOC: GraphicsBuffer[1] - BRG registration handle buffer for vegetation renderer - owner: HectonIndirectVegetationRenderer
            NativeArray<MetadataValue> batchMetadata = H8Memory.Allocate<MetadataValue>(
                BrgMetadataPlaceholderCount,
                VaultOwnerSystemId,
                TransientVegetationCullingAllocator);
            try
            {
                if (!batchMetadata.IsCreated)
                {
                    ReleaseBatchRendererGroupResources();
                    return;
                }

                _batchId = _batchRendererGroup.AddBatch(batchMetadata, _batchHandleBuffer.bufferHandle);
            }
            finally
            {
                if (batchMetadata.IsCreated)
                    H8Memory.Release(ref batchMetadata, VaultOwnerSystemId);
            }
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

            _nearBrgMaterial = sourceMaterial;
            if (_nearBrgMaterial == null)
                return false;

            bool useFarPass = farMesh != null;
            bool useDepthPass = _enableDepthPrepass && _depthOnlyMaterial != null;
            bool useDepthFarPass = useDepthPass && useFarPass;
            bool useShadowPass = _enableShadowCasterDraw && _shadowCasterMaterial != null;
            bool useMotionPass = _enableMotionVectorDraw && _motionVectorMaterial != null;
            bool useMotionFarPass = useMotionPass && useFarPass;
            if (!HasRequiredIndirectPropertyBlocks(
                    useFarPass,
                    useDepthPass,
                    useDepthFarPass,
                    useShadowPass,
                    useMotionPass,
                    useMotionFarPass))
            {
                return false;
            }

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
            float runtimeLodDistanceScalar = ResolveBrgLodDistanceScalar();
            float runtimeNearLodDistance = Mathf.Max(0.01f, _nearLodDistance * runtimeLodDistanceScalar);
            float runtimeFarLodDistance = Mathf.Max(runtimeNearLodDistance, _farLodDistance * runtimeLodDistanceScalar);
            float runtimeLodTransitionRange = Mathf.Max(0.01f, _lodTransitionRange * runtimeLodDistanceScalar);
            ApplyIndirectPropertyBlockBindings(ref _nearIndirectProperties, ref _nearMaterialBindingState, _nearBrgMaterial, activeInstanceDataBuffer, globalFloatingOffset, 0f, runtimeNearLodDistance, runtimeFarLodDistance, runtimeLodTransitionRange, _visibleIndicesLod0Buffer, true);
            ApplyIndirectPropertyBlockBindings(ref _farIndirectProperties, ref _farMaterialBindingState, _farBrgMaterial, activeInstanceDataBuffer, globalFloatingOffset, 1f, runtimeNearLodDistance, runtimeFarLodDistance, runtimeLodTransitionRange, _visibleIndicesLod1Buffer, true);
            ApplyIndirectPropertyBlockBindings(ref _depthNearIndirectProperties, ref _depthNearMaterialBindingState, _depthNearBrgMaterial, activeInstanceDataBuffer, globalFloatingOffset, 0f, runtimeNearLodDistance, runtimeFarLodDistance, runtimeLodTransitionRange, _visibleIndicesLod0Buffer, true);
            ApplyIndirectPropertyBlockBindings(ref _depthFarIndirectProperties, ref _depthFarMaterialBindingState, _depthFarBrgMaterial, activeInstanceDataBuffer, globalFloatingOffset, 1f, runtimeNearLodDistance, runtimeFarLodDistance, runtimeLodTransitionRange, _visibleIndicesLod1Buffer, true);
            ApplyIndirectPropertyBlockBindings(ref _shadowIndirectProperties, ref _shadowMaterialBindingState, _shadowBrgMaterial, activeInstanceDataBuffer, globalFloatingOffset, 0f, runtimeNearLodDistance, runtimeFarLodDistance, runtimeLodTransitionRange, _visibleIndicesShadowBuffer, true);
            ApplyIndirectPropertyBlockBindings(ref _motionNearIndirectProperties, ref _motionNearMaterialBindingState, _motionNearBrgMaterial, activeInstanceDataBuffer, globalFloatingOffset, 0f, runtimeNearLodDistance, runtimeFarLodDistance, runtimeLodTransitionRange, _visibleIndicesLod0Buffer, true);
            ApplyIndirectPropertyBlockBindings(ref _motionFarIndirectProperties, ref _motionFarMaterialBindingState, _motionFarBrgMaterial, activeInstanceDataBuffer, globalFloatingOffset, 1f, runtimeNearLodDistance, runtimeFarLodDistance, runtimeLodTransitionRange, _visibleIndicesLod1Buffer, true);
            return true;
        }

        private void ApplyIndirectPropertyBlockBindings(
            ref MaterialPropertyBlock propertyBlock,
            ref MaterialBindingState state,
            Material material,
            GraphicsBuffer activeInstanceDataBuffer,
            Vector4 globalFloatingOffset,
            float passMode,
            float runtimeNearLodDistance,
            float runtimeFarLodDistance,
            float runtimeLodTransitionRange,
            GraphicsBuffer visibleIndicesBuffer,
            bool useGpuIndirect)
        {
            if (material == null || propertyBlock == null || _instanceMatrixBuffer == null || activeInstanceDataBuffer == null)
            {
                state = default;
                return;
            }

            GraphicsBuffer floraAgeBuffer = TryResolveFloraAgeBufferHot();
            if (MaterialBindingStateMatches(
                    in state,
                    material,
                    activeInstanceDataBuffer,
                    floraAgeBuffer,
                    globalFloatingOffset,
                    passMode,
                    runtimeNearLodDistance,
                    runtimeFarLodDistance,
                    runtimeLodTransitionRange,
                    visibleIndicesBuffer,
                    useGpuIndirect))
            {
                // Bindings are still valid, but the view position is not: it moves every frame.
                // Folding it into MaterialBindingState would invalidate this cache every frame and
                // re-bind seven property blocks for the sake of one vector.
                PublishVegetationViewPosition(propertyBlock);
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
            float globalQualityWeight = math.saturate(math.select(1f, _cachedQualityWeight01, math.isfinite(_cachedQualityWeight01)));
            propertyBlock.SetVector(_RuntimeLodParamsId, new Vector4(passMode, runtimeNearLodDistance, runtimeFarLodDistance, runtimeLodTransitionRange));
            propertyBlock.SetVector(_RuntimeDrawParamsId, new Vector4(snapFlagsEnabled, _impostorWidth, _impostorHeight, useGpuIndirect && visibleIndicesBuffer != null ? 1f : 0f));
            propertyBlock.SetFloat(_H8GlobalQualityWeightId, globalQualityWeight);
            PublishVegetationViewPosition(propertyBlock);
            PublishInteractionAuthoring(propertyBlock);

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
                NearDistance = runtimeNearLodDistance,
                FarDistance = runtimeFarLodDistance,
                TransitionRange = runtimeLodTransitionRange,
                ImpostorWidth = _impostorWidth,
                ImpostorHeight = _impostorHeight,
                GlobalQualityWeight = globalQualityWeight,
                UseGpuIndirectFlag = ToBindingFlag(useGpuIndirect),
                IsValidFlag = BindingFlagTrue
            };
        }

        /// <summary>
        /// Publishes the culling camera position for the vertex animation. Deliberately the same
        /// position this renderer already culls with, so what bends and what is drawn agree, and
        /// deliberately NOT per-pass: a shadow pass reading _WorldSpaceCameraPos would read the light.
        /// </summary>
        private void PublishVegetationViewPosition(MaterialPropertyBlock propertyBlock)
        {
            Vector3 viewPosition = _cachedCullCameraPosition;
            if (!math.all(math.isfinite(new float3(viewPosition.x, viewPosition.y, viewPosition.z))))
            {
                // Leave w at 0 so the shader falls back to _WorldSpaceCameraPos rather than culling
                // every instance against a garbage origin.
                propertyBlock.SetVector(_VegetationViewPositionId, Vector4.zero);
                return;
            }

            propertyBlock.SetVector(
                _VegetationViewPositionId,
                new Vector4(viewPosition.x, viewPosition.y, viewPosition.z, 1f));
        }

        /// <summary>
        /// Copies the three authored interaction knobs off the lit material into this property block.
        /// They live in UnityPerMaterial, so they are per-material by construction and converting them
        /// to globals would have meant editing the CBUFFER layout and dropping the authored values on
        /// four separate materials. Pushing them through the property block instead keeps the lit
        /// material as the single authored source and mutates no asset.
        ///
        /// Re-published only when bindings are rebuilt, which is when the material can have changed.
        /// </summary>
        private void PublishInteractionAuthoring(MaterialPropertyBlock propertyBlock)
        {
            Material authoringMaterial = _material;
            if (authoringMaterial == null)
                return;

            propertyBlock.SetFloat(_InteractionPushStrengthId, authoringMaterial.GetFloat(_InteractionPushStrengthId));
            propertyBlock.SetFloat(_InteractionVelocityBiasId, authoringMaterial.GetFloat(_InteractionVelocityBiasId));
            propertyBlock.SetFloat(_InteractionDistancePowerId, authoringMaterial.GetFloat(_InteractionDistancePowerId));
        }

        private bool MaterialBindingStateMatches(
            in MaterialBindingState state,
            Material material,
            GraphicsBuffer activeInstanceDataBuffer,
            GraphicsBuffer floraAgeBuffer,
            Vector4 globalFloatingOffset,
            float passMode,
            float runtimeNearLodDistance,
            float runtimeFarLodDistance,
            float runtimeLodTransitionRange,
            GraphicsBuffer visibleIndicesBuffer,
            bool useGpuIndirect)
        {
            float globalQualityWeight = math.saturate(math.select(1f, _cachedQualityWeight01, math.isfinite(_cachedQualityWeight01)));
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
                state.NearDistance == runtimeNearLodDistance &&
                state.FarDistance == runtimeFarLodDistance &&
                state.TransitionRange == runtimeLodTransitionRange &&
                state.ImpostorWidth == _impostorWidth &&
                state.ImpostorHeight == _impostorHeight &&
                state.GlobalQualityWeight == globalQualityWeight &&
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
                !_supportsComputeShadersCold ||
                cullCamera == null ||
                nearMesh == null ||
                _cullingCompute == null ||
                _clearIndirectArgsKernel < 0 ||
                _instanceMatrixBuffer == null ||
                _instanceCount <= 0)
            {
                return false;
            }

            GraphicsBuffer activeInstanceDataBuffer = TryResolveActiveInstanceDataBufferHot();
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

            if (!HasGpuIndirectResources(_instanceCount))
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
            float brgLodDistanceScalar = ResolveBrgLodDistanceScalar();
            float brgNearLodDistance = Mathf.Max(0.01f, _nearLodDistance * brgLodDistanceScalar);
            float brgFarLodDistance = Mathf.Max(brgNearLodDistance, _farLodDistance * brgLodDistanceScalar);
            float brgLodTransitionRange = Mathf.Max(0.01f, _lodTransitionRange * brgLodDistanceScalar);
            float densityKeepProbability01 = ResolveDensityKeepProbability01();
            int densityDecimationStep = ResolveDensityDecimationStep(densityKeepProbability01);
            _resolvedDensityDecimationStep = densityDecimationStep;
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

            GraphicsBuffer floraAgeBuffer = TryResolveFloraAgeBufferHot();
            if (floraAgeBuffer == null)
                return;

            bool runShadowCull = _visibleIndicesShadowBuffer != null && _cullFloraShadowKernel >= 0;
            int dispatchGroups = CeilDividePositive(_instanceCount, _cullFloraThreadGroupSizeX);
            int shadowDispatchGroups = runShadowCull
                ? CeilDividePositive(_instanceCount, _cullFloraShadowThreadGroupSizeX)
                : 0;
            if (dispatchGroups <= 0 || (runShadowCull && shadowDispatchGroups <= 0))
                return;

            ApplyCullComputeBindings(
                ref _mainCullComputeBindingState,
                _cullFloraKernel,
                activeInstanceDataBuffer,
                floraAgeBuffer,
                shadowKernel: false);
            _cullingCompute.SetInt(_FarLodAppendEnabledId, updateFarLodThisFrame ? 1 : 0);
            _cullingCompute.SetInt(_DensityDecimationStepId, densityDecimationStep);
            _cullingCompute.SetFloat(_DensityKeepProbabilityId, densityKeepProbability01);
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
            _cullingCompute.SetVector(_OcclusionZBufferParamsId, _cachedZBufferParams);
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
            _cullingCompute.SetFloat(_FloorBiolumStrengthId, _cachedFloorBiolumStrength);
            _cullingCompute.SetFloat(_OceanBiolumStrengthId, _cachedOceanBiolumStrength);
            _cullingCompute.SetFloat(_BiolumIntensityVectorId, ResolveBiolumIntensityScalar());

            DispatchFloraSnapFlagUpdate(activeInstanceDataBuffer, globalFloatingOffset);
            _cullingCompute.Dispatch(_cullFloraKernel, dispatchGroups, 1, 1);

            if (runShadowCull)
            {
                ApplyCullComputeBindings(
                    ref _shadowCullComputeBindingState,
                    _cullFloraShadowKernel,
                    activeInstanceDataBuffer,
                    floraAgeBuffer,
                    shadowKernel: true);
                _cullingCompute.SetInt(_DensityDecimationStepId, densityDecimationStep);
                _cullingCompute.SetFloat(_DensityKeepProbabilityId, densityKeepProbability01);
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
                _cullingCompute.Dispatch(_cullFloraShadowKernel, shadowDispatchGroups, 1, 1);
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

        private void DispatchFloraSnapFlagUpdate(GraphicsBuffer activeInstanceDataBuffer, Vector4 globalFloatingOffset)
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
                int clearGroups = CeilDividePositive(_instanceCount, _clearFloraSnapFlagsThreadGroupSizeX);
                if (clearGroups <= 0)
                    return;

                ApplySnapComputeBindings(
                    ref _clearSnapComputeBindingState,
                    _clearFloraSnapFlagsKernel,
                    activeInstanceDataBuffer,
                    clearKernel: true);
                _abyssalFlowFieldCompute.SetInt(_SourceInstanceCountId, _instanceCount);
                _abyssalFlowFieldCompute.Dispatch(_clearFloraSnapFlagsKernel, clearGroups, 1, 1);
                _floraSnapFlagBufferRequiresClear = false;
            }

            Vector4 washVelocity = _cachedSubmarineWashVelocity;
            Vector4 washSphere = _cachedSubmarineWashSphere;
            if (washVelocity.w <= 10f || washSphere.w <= 0f)
                return;

            int flagGroups = CeilDividePositive(_instanceCount, _flagSnappedFloraThreadGroupSizeX);
            if (flagGroups <= 0)
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
            _abyssalFlowFieldCompute.Dispatch(_flagSnappedFloraKernel, flagGroups, 1, 1);
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
            if (!_supportsComputeShadersCold ||
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

            DispatchFloraSnapFlagUpdate(activeInstanceDataBuffer, globalFloatingOffset);
        }

        private bool BuildDepthPyramid(Camera cullCamera)
        {
            if (!_enableDepthOcclusion || _depthPyramidCompute == null || cullCamera == null)
                return false;

            Texture depthTexture = _cachedCameraDepthTexture;
            if (depthTexture == null)
                return false;

            int targetWidth = Mathf.Max(1, cullCamera.pixelWidth);
            int targetHeight = Mathf.Max(1, cullCamera.pixelHeight);
            if (!HasDepthPyramidResources(targetWidth, targetHeight))
                return false;

            int copyGroupsX = CeilDividePositive(_depthPyramidWidth, _depthPyramidCopyThreadGroupSizeX);
            int copyGroupsY = CeilDividePositive(_depthPyramidHeight, _depthPyramidCopyThreadGroupSizeY);
            if (copyGroupsX <= 0 || copyGroupsY <= 0)
                return false;

            _depthPyramidCompute.SetTexture(_depthPyramidCopyKernel, _DepthPyramidSourceDepthId, depthTexture);
            _depthPyramidCompute.SetTexture(_depthPyramidCopyKernel, _DepthPyramidTargetId, _depthPyramidTexture, 0);
            _depthPyramidCompute.Dispatch(
                _depthPyramidCopyKernel,
                copyGroupsX,
                copyGroupsY,
                1);

            for (int mipIndex = 1; mipIndex < _depthPyramidMipCount; mipIndex++)
            {
                int mipWidth = Mathf.Max(1, _depthPyramidWidth >> mipIndex);
                int mipHeight = Mathf.Max(1, _depthPyramidHeight >> mipIndex);
                int downsampleGroupsX = CeilDividePositive(mipWidth, _depthPyramidDownsampleThreadGroupSizeX);
                int downsampleGroupsY = CeilDividePositive(mipHeight, _depthPyramidDownsampleThreadGroupSizeY);
                if (downsampleGroupsX <= 0 || downsampleGroupsY <= 0)
                    return false;

                _depthPyramidCompute.SetTexture(_depthPyramidDownsampleKernel, _DepthPyramidSourceId, _depthPyramidTexture, mipIndex - 1);
                _depthPyramidCompute.SetTexture(_depthPyramidDownsampleKernel, _DepthPyramidTargetId, _depthPyramidTexture, mipIndex);
                _depthPyramidCompute.Dispatch(
                    _depthPyramidDownsampleKernel,
                    downsampleGroupsX,
                    downsampleGroupsY,
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

        private int ResolveKernel(ComputeShader computeShader, string kernelName)
        {
            if (computeShader == null || !_supportsComputeShadersCold)
                return -1;

            try
            {
                if (!computeShader.HasKernel(kernelName))
                    return -1;

                int kernel = computeShader.FindKernel(kernelName);
                if (kernel < 0)
                    return -1;

                return computeShader.IsSupported(kernel) ? kernel : -1;
            }
            catch (System.ObjectDisposedException)
            {
                return -1;
            }
            catch (System.InvalidOperationException)
            {
                return -1;
            }
            catch (System.ArgumentException)
            {
                return -1;
            }
            catch (MissingReferenceException)
            {
                return -1;
            }
            catch (UnityException)
            {
                return -1;
            }
        }

        private int ResolveKernelThreadGroupSizeX(ComputeShader computeShader, int kernel)
        {
            if (computeShader == null ||
                kernel < 0 ||
                !_supportsComputeShadersCold)
                return 0;

            uint queryX;
            uint queryY;
            uint queryZ;
            try
            {
                if (!computeShader.IsSupported(kernel))
                    return 0;

                computeShader.GetKernelThreadGroupSizes(kernel, out queryX, out queryY, out queryZ);
            }
            catch (System.ObjectDisposedException)
            {
                return 0;
            }
            catch (System.InvalidOperationException)
            {
                return 0;
            }
            catch (System.ArgumentException)
            {
                return 0;
            }
            catch (MissingReferenceException)
            {
                return 0;
            }
            catch (UnityException)
            {
                return 0;
            }
            if (queryX == 0u || queryY != 1u || queryZ != 1u || queryX > int.MaxValue)
                return 0;

            ulong totalThreads = queryX * (ulong)queryY * queryZ;
            return totalThreads <= PortableMaxComputeThreadsPerGroup ? (int)queryX : 0;
        }

        private void ResolveKernelThreadGroupSizes(
            ComputeShader computeShader,
            int kernel,
            out int threadGroupSizeX,
            out int threadGroupSizeY)
        {
            threadGroupSizeX = 0;
            threadGroupSizeY = 0;
            if (computeShader == null ||
                kernel < 0 ||
                !_supportsComputeShadersCold)
                return;

            uint queryX;
            uint queryY;
            uint queryZ;
            try
            {
                if (!computeShader.IsSupported(kernel))
                    return;

                computeShader.GetKernelThreadGroupSizes(kernel, out queryX, out queryY, out queryZ);
            }
            catch (System.ObjectDisposedException)
            {
                return;
            }
            catch (System.InvalidOperationException)
            {
                return;
            }
            catch (System.ArgumentException)
            {
                return;
            }
            catch (MissingReferenceException)
            {
                return;
            }
            catch (UnityException)
            {
                return;
            }
            if (queryX == 0u || queryY == 0u || queryZ != 1u || queryX > int.MaxValue || queryY > int.MaxValue)
                return;

            ulong totalThreads = queryX * (ulong)queryY * queryZ;
            if (totalThreads > PortableMaxComputeThreadsPerGroup)
                return;

            threadGroupSizeX = (int)queryX;
            threadGroupSizeY = (int)queryY;
        }

        private static int CeilDividePositive(int value, int divisor)
        {
            const int MaxDispatchGroupsPerDimension = 65535;
            if (value <= 0 || divisor <= 0)
                return 0;

            long groups = ((long)value + divisor - 1L) / divisor;
            return groups <= MaxDispatchGroupsPerDimension ? (int)groups : 0;
        }

        private float ResolveBiolumIntensityScalar()
        {
            return _cachedBiolumIntensityScalar;
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

        private bool HasGpuIndirectResources(int instanceCount)
        {
            int requiredCapacity = Mathf.NextPowerOfTwo(Mathf.Max(1, instanceCount));
            bool hasVisibleIndexBuffers =
                _gpuVisibleIndexCapacity >= requiredCapacity &&
                IsValidBuffer(_visibleIndicesLod0Buffer) &&
                IsValidBuffer(_visibleIndicesLod1Buffer) &&
                IsValidBuffer(_visibleIndicesShadowBuffer);
            bool hasIndirectArgsBuffers =
                IsValidBuffer(_indirectArgsLod0Buffer) &&
                IsValidBuffer(_indirectArgsLod1Buffer) &&
                IsValidBuffer(_indirectArgsShadowBuffer);
            bool hasTelemetryBuffers =
                IsValidBuffer(_cullTelemetryCountersBuffer) &&
                IsValidBuffer(_cullTelemetryCountersUploadBuffer);
            bool needsFloraSnapFlags =
                _abyssalFlowFieldCompute != null &&
                _clearFloraSnapFlagsKernel >= 0 &&
                _flagSnappedFloraKernel >= 0;
            bool hasFloraSnapFlags =
                !needsFloraSnapFlags ||
                (IsValidBuffer(_floraSnapFlagBuffer) && _floraSnapFlagCapacity >= requiredCapacity);

            return hasVisibleIndexBuffers &&
                   hasIndirectArgsBuffers &&
                   hasTelemetryBuffers &&
                   hasFloraSnapFlags;
        }

        private static bool IsValidBuffer(GraphicsBuffer buffer)
        {
            return buffer != null && buffer.IsValid();
        }

        private void EnsureIndirectArgsBuffer(ref GraphicsBuffer argsBuffer)
        {
            if (argsBuffer == null)
                argsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, GraphicsBuffer.IndirectDrawIndexedArgs.size); // COLD ALLOC: GraphicsBuffer[1] - GPU-cleared indirect indexed draw arguments for vegetation pass - owner: HectonIndirectVegetationRenderer
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
                if (_cullTelemetryCountersUploadBuffer == null || !_cullTelemetryCountersUploadBuffer.IsValid())
                    _cullTelemetryCountersUploadBuffer = GraphicsBufferUploadUtility.CreateStructuredUploadStagingBuffer<uint>(ScatterCullTelemetryCounterCount);
                return;
            }

            bool keepCullTelemetryCountersBuffer = DeferPendingScatterCullTelemetryReadbackForRelease();
            if (keepCullTelemetryCountersBuffer)
                _cullTelemetryCountersBuffer = null;
            else
                ReleaseGraphicsBuffer(ref _cullTelemetryCountersBuffer);
            ReleaseGraphicsBuffer(ref _cullTelemetryCountersUploadBuffer);
            _cullTelemetryCountersBuffer = GraphicsBufferUploadUtility.CreateStructuredCopyDestinationBuffer<uint>(ScatterCullTelemetryCounterCount); // COLD ALLOC: GraphicsBuffer[4] - GPU cull telemetry counters for SHINOBU_09 scatter diagnostics - owner: HectonIndirectVegetationRenderer
            _cullTelemetryCountersUploadBuffer = GraphicsBufferUploadUtility.CreateStructuredUploadStagingBuffer<uint>(ScatterCullTelemetryCounterCount); // COLD ALLOC: GraphicsBuffer[4] - CPU-visible telemetry clear staging, GPU copy source only - owner: HectonIndirectVegetationRenderer
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
            if (_floraAgeBuffer == null || !TryReadFloraAges(out NativeArray<float> floraAges))
                return null;

            if (_floraAgeBufferDirty)
            {
                if (!_hasCpuCullingData && !_floraAgesAuthoredExternally)
                    FillDefaultFloraAges(_instanceCount);

                RecordFloraGrowthTelemetry(_instanceCount, true);
                if (_floraAgesAuthoredExternally)
                    SanitizeFloraAgeBufferForUpload(_instanceCount);
                if (!TryReadFloraAges(out floraAges))
                    return null;

                GraphicsBufferUploadUtility.UploadNativeArray(_floraAgeBuffer, floraAges, _instanceCount);
                _floraAgeBufferDirty = false;
            }
            else
            {
                RecordFloraGrowthTelemetry(_instanceCount, false);
            }

            return _floraAgeBuffer;
        }

        private GraphicsBuffer TryResolveFloraAgeBufferHot()
        {
            if (_instanceCount <= 0 ||
                _floraAgeBufferDirty ||
                !IsValidBuffer(_floraAgeBuffer) ||
                _floraAgeCapacity < _instanceCount)
            {
                return null;
            }

            return TryReadFloraAges(out NativeArray<float> floraAges) && floraAges.Length >= _instanceCount
                ? _floraAgeBuffer
                : null;
        }

        private void EnsureFloraAgeCapacity(int requiredCount)
        {
            if (requiredCount <= 0)
                return;

            int requiredCapacity = Mathf.NextPowerOfTwo(Mathf.Max(16, requiredCount));
            if (_floraAgeBuffer != null &&
                _floraAgeBuffer.IsValid() &&
                _floraAgeCapacity >= requiredCapacity &&
                TryReadFloraAges(out NativeArray<float> existingAges) &&
                existingAges.Length >= requiredCapacity)
            {
                return;
            }

            EnsureVaultStorage(ref _floraAges01Handle, FloraAgeBufferId, requiredCapacity, NativeArrayOptions.UninitializedMemory);
            if (_floraAgeBuffer == null || !_floraAgeBuffer.IsValid() || _floraAgeCapacity < requiredCapacity)
            {
                ReleaseGraphicsBuffer(ref _floraAgeBuffer);
                _floraAgeBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float>(requiredCapacity); // COLD ALLOC: GraphicsBuffer[NextPowerOfTwo(instanceCount)] - StructuredBuffer<float> flora age lane - owner: HectonIndirectVegetationRenderer
            }

            _floraAgeCapacity = requiredCapacity;
            FillDefaultFloraAges(requiredCapacity);
            _floraAgeBufferDirty = true;
        }

        private void FillDefaultFloraAges(int count)
        {
            if (!TryAcquireFloraAgesForWrite(count, out IDataVault vault, out NativeArray<float> floraAges))
                return;

            try
            {
                int safeCount = Mathf.Min(count, floraAges.Length);
                for (int instanceIndex = 0; instanceIndex < safeCount; instanceIndex++)
                    floraAges[instanceIndex] = 1f;
            }
            finally
            {
                vault.ReleaseWriteLock(in _floraAges01Handle, VaultOwnerSystemId);
            }
        }

        private void SanitizeFloraAgeBufferForUpload(int count)
        {
            if (!TryAcquireFloraAgesForWrite(count, out IDataVault vault, out NativeArray<float> floraAges))
                return;

            try
            {
                int safeCount = math.min(count, floraAges.Length);
                for (int instanceIndex = 0; instanceIndex < safeCount; instanceIndex++)
                    floraAges[instanceIndex] = SanitizeFloraAgeForUpload(floraAges[instanceIndex]);
            }
            finally
            {
                vault.ReleaseWriteLock(in _floraAges01Handle, VaultOwnerSystemId);
            }
        }

        private void ReleaseFloraAgeResources()
        {
            ReleaseGraphicsBuffer(ref _floraAgeBuffer);
            ReleaseVaultHandle(_dataVault, ref _floraAges01Handle);

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

        private bool EnsureFloraGrowthTelemetry()
        {
            return EnsureTelemetryBuffer(
                ref _floraGrowthTelemetryHandle,
                FloraGrowthTelemetryBufferId,
                FloraGrowthTelemetryFrameCount);
        }

        private bool TryAcquireFloraGrowthTelemetry(out IDataVault vault, out NativeArray<FloraGrowthTelemetryEntry> floraGrowthTelemetry)
        {
            return TryAcquireExistingTelemetryBuffer(
                in _floraGrowthTelemetryHandle,
                FloraGrowthTelemetryBufferId,
                FloraGrowthTelemetryFrameCount,
                out vault,
                out floraGrowthTelemetry);
        }

        private bool TryReadFloraGrowthTelemetry(out NativeArray<FloraGrowthTelemetryEntry>.ReadOnly floraGrowthTelemetry)
        {
            return TryReadTelemetryBuffer(
                in _floraGrowthTelemetryHandle,
                FloraGrowthTelemetryBufferId,
                FloraGrowthTelemetryFrameCount,
                out floraGrowthTelemetry);
        }

        private void ReleaseFloraGrowthTelemetryResources()
        {
            ReleaseFloraGrowthTelemetryResources(_dataVault);
        }

        private void ReleaseFloraGrowthTelemetryResources(IDataVault vault)
        {
            ReleaseVaultHandle(vault, ref _floraGrowthTelemetryHandle);
            _floraGrowthTelemetryCursor = 0;
            _lastFloraGrowthTelemetryFrame = -1;
        }

        private void RecordFloraGrowthTelemetry(int instanceCount, bool fullScan)
        {
            if (instanceCount <= 0 || !TryReadFloraAges(out NativeArray<float> floraAges))
                return;

            int frameIndex = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (_lastFloraGrowthTelemetryFrame == frameIndex)
                return;

            _lastFloraGrowthTelemetryFrame = frameIndex;
            if (!TryAcquireFloraGrowthTelemetry(out IDataVault vault, out NativeArray<FloraGrowthTelemetryEntry> floraGrowthTelemetry))
                return;

            int nanCount = 0;
            try
            {
                int safeCount = math.min(instanceCount, floraAges.Length);
                int sampleLimit = fullScan ? safeCount : math.min(safeCount, FloraGrowthTelemetryMaxSamples);
                int stride = sampleLimit > 0 ? math.max(1, (safeCount + sampleLimit - 1) / sampleLimit) : 1;
                int sampled = 0;
                int negativeCount = 0;
                uint ageHash = FloraGrowthTelemetryHashSeed;
                float minAge = 1f;
                float maxAge = 0f;

                for (int instanceIndex = 0; instanceIndex < safeCount; instanceIndex += stride)
                {
                    float age = floraAges[instanceIndex];
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
                floraGrowthTelemetry[writeIndex] = new FloraGrowthTelemetryEntry
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
            }
            finally
            {
                vault.ReleaseWriteLock(in _floraGrowthTelemetryHandle, VaultOwnerSystemId);
            }

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
            if (_floraGrowthTelemetryDumped)
                return;

            _floraGrowthTelemetryDumped = true;
            if (!TryReadFloraGrowthTelemetry(out NativeArray<FloraGrowthTelemetryEntry>.ReadOnly floraGrowthTelemetry))
                return;

            _ = floraGrowthTelemetry;
        }

        private bool BeginCullTelemetrySample()
        {
            if (!_enableCullTelemetry ||
                _cullTelemetryCountersBuffer == null ||
                _cullTelemetryCountersUploadBuffer == null ||
                _cullTelemetryClearPayload == null ||
                _scatterCullTelemetryReadbackPending)
            {
                return false;
            }

            int frameIndex = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (frameIndex == _lastScatterCullTelemetrySampleFrame ||
                frameIndex % ScatterCullTelemetryReadbackStrideFrames != 0)
            {
                return false;
            }

            _lastScatterCullTelemetrySampleFrame = frameIndex;
            GraphicsBufferUploadUtility.UploadArrayAndCopyWholeBuffer(
                _cullTelemetryCountersUploadBuffer,
                _cullTelemetryCountersBuffer,
                _cullTelemetryClearPayload,
                ScatterCullTelemetryCounterCount);
            return true;
        }

        private void EnsureDepthPyramidResourcesForCameraCold(Camera cullCamera)
        {
            if (!_enableDepthOcclusion ||
                _depthPyramidCompute == null ||
                cullCamera == null ||
                !_supportsComputeShadersCold ||
                _cachedCameraDepthTexture == null)
            {
                return;
            }

            EnsureDepthPyramidResources(
                Mathf.Max(1, cullCamera.pixelWidth),
                Mathf.Max(1, cullCamera.pixelHeight));
        }

        private bool HasDepthPyramidResources(int targetWidth, int targetHeight)
        {
            return _depthPyramidTexture != null &&
                   _depthPyramidWidth == targetWidth &&
                   _depthPyramidHeight == targetHeight &&
                   _depthPyramidCopyKernel >= 0 &&
                   _depthPyramidDownsampleKernel >= 0;
        }

        private Vector4 ResolveZBufferParams(Camera cullCamera)
        {
            float nearClip = cullCamera != null ? cullCamera.nearClipPlane : 0.01f;
            float farClip = cullCamera != null ? cullCamera.farClipPlane : 1000f;
            nearClip = math.max(0.0001f, math.isfinite(nearClip) ? nearClip : 0.01f);
            farClip = math.max(nearClip + 0.0001f, math.isfinite(farClip) ? farClip : 1000f);
            float farOverNear = farClip * math.rcp(nearClip);

            if (_usesReversedZBufferCold)
            {
                float reversedX = farOverNear - 1f;
                return new Vector4(reversedX, 1f, reversedX * math.rcp(farClip), math.rcp(farClip));
            }

            float forwardX = 1f - farOverNear;
            return new Vector4(
                forwardX,
                farOverNear,
                forwardX * math.rcp(farClip),
                farOverNear * math.rcp(farClip));
        }

        private void RequestCullTelemetryReadback(bool sampleCullTelemetry)
        {
            if (!sampleCullTelemetry ||
                _cullTelemetryCountersBuffer == null ||
                _scatterCullTelemetryReadbackPending ||
                _scatterCullTelemetryReadbackDisposeAfterCompletion)
                return;

            if (!HasCullTelemetryReadbackData())
            {
                QueueCullTelemetryReadbackRepair();
                return;
            }

            _cullTelemetryReadbackRequest = AsyncGPUReadback.RequestIntoNativeArray(
                ref _cullTelemetryReadback.Data,
                _cullTelemetryCountersBuffer,
                ResolveScatterCullTelemetryReadbackCompletion());
            _scatterCullTelemetryReadbackPending = !_cullTelemetryReadbackRequest.hasError;
            if (!_scatterCullTelemetryReadbackPending)
                _cullTelemetryReadbackRequest = default;
        }

        private void PollCullTelemetryReadback()
        {
            if (!_scatterCullTelemetryReadbackPending || !_cullTelemetryReadbackRequest.done)
                return;

            _scatterCullTelemetryReadbackPending = false;
            bool readbackError = _cullTelemetryReadbackRequest.hasError;
            _cullTelemetryReadbackRequest = default;
            if (readbackError)
                return;

            NativeArray<uint> counters = _cullTelemetryReadback.Data;
            if (!counters.IsCreated || counters.Length < ScatterCullTelemetryCounterCount)
                return;

            int totalCount = ClampCounterToInt(counters[ScatterCullTelemetryTotalCounter]);
            int frustumCount = ClampCounterToInt(counters[ScatterCullTelemetryFrustumCounter]);
            int occlusionCount = ClampCounterToInt(counters[ScatterCullTelemetryOcclusionCounter]);
            int visibleCount = ClampCounterToInt(counters[ScatterCullTelemetryVisibleCounter]);
            RecordScatterCullTelemetry(totalCount, frustumCount, occlusionCount, visibleCount);
        }

        private bool EnsureScatterCullTelemetry()
        {
            return EnsureTelemetryBuffer(
                ref _scatterCullTelemetryHandle,
                ScatterCullTelemetryBufferId,
                ScatterCullTelemetryFrameCount);
        }

        private bool TryAcquireScatterCullTelemetry(out IDataVault vault, out NativeArray<ScatterCullTelemetryEntry> scatterCullTelemetry)
        {
            return TryAcquireExistingTelemetryBuffer(
                in _scatterCullTelemetryHandle,
                ScatterCullTelemetryBufferId,
                ScatterCullTelemetryFrameCount,
                out vault,
                out scatterCullTelemetry);
        }

        private bool TryReadScatterCullTelemetry(out NativeArray<ScatterCullTelemetryEntry>.ReadOnly scatterCullTelemetry)
        {
            return TryReadTelemetryBuffer(
                in _scatterCullTelemetryHandle,
                ScatterCullTelemetryBufferId,
                ScatterCullTelemetryFrameCount,
                out scatterCullTelemetry);
        }

        private void RecordScatterCullTelemetry(int totalCount, int frustumCount, int occlusionCount, int visibleCount)
        {
            int frameIndex = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (_lastScatterCullTelemetryFrame == frameIndex)
                return;

            _lastScatterCullTelemetryFrame = frameIndex;
            if (!TryAcquireScatterCullTelemetry(out IDataVault vault, out NativeArray<ScatterCullTelemetryEntry> scatterCullTelemetry))
                return;

            bool invalidCounterState =
                totalCount < 0 ||
                frustumCount < 0 ||
                occlusionCount < 0 ||
                visibleCount < 0 ||
                visibleCount > totalCount + _resolvedDensityDecimationStep;
            _lastCullOverdrawWarning = visibleCount > ScatterCullOverdrawWarningVisibleCount;

            try
            {
                scatterCullTelemetry[_scatterCullTelemetryCursor] = new ScatterCullTelemetryEntry
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
            }
            finally
            {
                vault.ReleaseWriteLock(in _scatterCullTelemetryHandle, VaultOwnerSystemId);
            }

            if (invalidCounterState && !_scatterCullTelemetryDumped)
                DumpScatterCullTelemetry();
        }

        private void ReleaseScatterCullTelemetryResources()
        {
            ReleaseScatterCullTelemetryResources(_dataVault);
        }

        private void ReleaseScatterCullTelemetryResources(IDataVault vault)
        {
            ReleaseVaultHandle(vault, ref _scatterCullTelemetryHandle);
            _scatterCullTelemetryCursor = 0;
            _lastScatterCullTelemetryFrame = -1;
            _lastScatterCullTelemetrySampleFrame = -1;
            _scatterCullTelemetryReadbackPending = false;
            _scatterCullTelemetryReadbackRepairRequested = false;
        }

        private void DumpScatterCullTelemetry()
        {
            if (_scatterCullTelemetryDumped)
                return;

            _scatterCullTelemetryDumped = true;
            if (!TryReadScatterCullTelemetry(out NativeArray<ScatterCullTelemetryEntry>.ReadOnly scatterCullTelemetry))
                return;

            _ = scatterCullTelemetry;
        }

        private bool EnsureTelemetryBuffer<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int length)
            where T : struct
        {
            IDataVault vault = _dataVault;
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            if (IsExactVaultHandle(in handle, bufferId))
                return !vault.IsCompactionFenceActive;

            handle = vault.EnsureGenerationHandle<T>(
                bufferId,
                length,
                VaultOwnerSystemId,
                NativeArrayOptions.ClearMemory);
            return !vault.IsCompactionFenceActive &&
                   IsExactVaultHandle(in handle, bufferId);
        }

        private bool EnsureVaultStorage<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int length,
            NativeArrayOptions options)
            where T : struct
        {
            IDataVault vault = _dataVault;
            if (vault == null || vault.IsCompactionFenceActive || length <= 0)
                return false;

            if (IsExactVaultHandle(in handle, bufferId) &&
                !vault.IsCompactionFenceActive &&
                vault.TryReadOnlyHandle(in handle, out NativeArray<T>.ReadOnly existing) &&
                existing.IsCreated &&
                existing.Length >= length &&
                !vault.IsCompactionFenceActive)
            {
                return true;
            }

            handle = vault.EnsureGenerationHandle<T>(
                bufferId,
                length,
                VaultOwnerSystemId,
                options);
            return !vault.IsCompactionFenceActive &&
                   IsExactVaultHandle(in handle, bufferId) &&
                   vault.TryReadOnlyHandle(in handle, out NativeArray<T>.ReadOnly resolved) &&
                   resolved.IsCreated &&
                   resolved.Length >= length &&
                   !vault.IsCompactionFenceActive;
        }

        private bool TryReadFloraAges(out NativeArray<float> floraAges)
        {
            floraAges = default;
            IDataVault vault = _dataVault;
            return vault != null &&
                   !vault.IsCompactionFenceActive &&
                   IsExactVaultHandle(in _floraAges01Handle, FloraAgeBufferId) &&
                   vault.TryResolveHandle(in _floraAges01Handle, out floraAges) &&
                   floraAges.IsCreated &&
                   !vault.IsCompactionFenceActive;
        }

        private bool TryReadFloraAges(out NativeArray<float>.ReadOnly floraAges)
        {
            floraAges = default;
            IDataVault vault = _dataVault;
            return vault != null &&
                   !vault.IsCompactionFenceActive &&
                   IsExactVaultHandle(in _floraAges01Handle, FloraAgeBufferId) &&
                   vault.TryReadOnlyHandle(in _floraAges01Handle, out floraAges) &&
                   floraAges.IsCreated &&
                   !vault.IsCompactionFenceActive;
        }

        private bool TryAcquireFloraAgesForWrite(int requiredCount, out IDataVault vault, out NativeArray<float> floraAges)
        {
            vault = null;
            floraAges = default;
            if (!EnsureVaultStorage(ref _floraAges01Handle, FloraAgeBufferId, math.max(1, requiredCount), NativeArrayOptions.UninitializedMemory))
                return false;

            vault = _dataVault;
            if (vault == null ||
                vault.IsCompactionFenceActive)
            {
                vault = null;
                return false;
            }

            bool lockAcquired = false;
            bool success = false;
            try
            {
                if (!vault.TryAcquireWriteLock(in _floraAges01Handle, VaultOwnerSystemId, out floraAges))
                    return false;

                lockAcquired = true;
                success =
                    !vault.IsCompactionFenceActive &&
                    floraAges.IsCreated &&
                    floraAges.Length >= requiredCount;
                return success;
            }
            finally
            {
                if (!success && lockAcquired && vault != null)
                    vault.ReleaseWriteLock(in _floraAges01Handle, VaultOwnerSystemId);

                if (!success)
                {
                    floraAges = default;
                    vault = null;
                }
            }
        }

        private bool TryReadCpuCullingData(
            int requiredCount,
            out NativeArray<Matrix4x4>.ReadOnly matrices,
            out NativeArray<HectonVegetationInstanceData>.ReadOnly instanceData)
        {
            matrices = default;
            instanceData = default;
            IDataVault vault = _dataVault;
            return requiredCount > 0 &&
                   vault != null &&
                   !vault.IsCompactionFenceActive &&
                   IsExactVaultHandle(in _cpuCullingMatricesHandle, CpuCullingMatricesBufferId) &&
                   IsExactVaultHandle(in _cpuCullingDataHandle, CpuCullingDataBufferId) &&
                   vault.TryReadOnlyHandle(in _cpuCullingMatricesHandle, out matrices) &&
                   vault.TryReadOnlyHandle(in _cpuCullingDataHandle, out instanceData) &&
                   matrices.IsCreated &&
                   instanceData.IsCreated &&
                   matrices.Length >= requiredCount &&
                   instanceData.Length >= requiredCount &&
                   !vault.IsCompactionFenceActive;
        }

        private bool TryAcquireCpuCullingMatricesForWrite(
            int requiredCount,
            out IDataVault vault,
            out NativeArray<Matrix4x4> matrices)
        {
            return TryAcquireVaultStorageForWrite(
                ref _cpuCullingMatricesHandle,
                CpuCullingMatricesBufferId,
                requiredCount,
                NativeArrayOptions.UninitializedMemory,
                out vault,
                out matrices);
        }

        private bool TryAcquireCpuCullingInstanceDataForWrite(
            int requiredCount,
            out IDataVault vault,
            out NativeArray<HectonVegetationInstanceData> instanceData)
        {
            return TryAcquireVaultStorageForWrite(
                ref _cpuCullingDataHandle,
                CpuCullingDataBufferId,
                requiredCount,
                NativeArrayOptions.UninitializedMemory,
                out vault,
                out instanceData);
        }

        private bool TryAcquireVaultStorageForWrite<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredCount,
            NativeArrayOptions clearOptions,
            out IDataVault vault,
            out NativeArray<T> buffer)
            where T : struct
        {
            vault = null;
            buffer = default;
            int safeCount = math.max(1, requiredCount);
            if (!EnsureVaultStorage(ref handle, bufferId, safeCount, clearOptions))
                return false;

            vault = _dataVault;
            bool lockAcquired = false;
            bool success = false;
            if (vault == null ||
                vault.IsCompactionFenceActive)
            {
                vault = null;
                return false;
            }

            try
            {
                if (!vault.TryAcquireWriteLock(in handle, VaultOwnerSystemId, out buffer))
                    return false;

                lockAcquired = true;
                success =
                    !vault.IsCompactionFenceActive &&
                    buffer.IsCreated &&
                    buffer.Length >= requiredCount;
                return success;
            }
            finally
            {
                if (!success && lockAcquired && vault != null)
                    vault.ReleaseWriteLock(in handle, VaultOwnerSystemId);

                if (!success)
                {
                    vault = null;
                    buffer = default;
                }
            }
        }

        private bool TryAcquireTelemetryBuffer<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int length,
            out IDataVault vault,
            out NativeArray<T> buffer)
            where T : struct
        {
            vault = null;
            buffer = default;
            if (!EnsureTelemetryBuffer(ref handle, bufferId, length))
                return false;

            vault = _dataVault;
            bool lockAcquired = false;
            bool success = false;
            if (vault == null ||
                vault.IsCompactionFenceActive)
            {
                buffer = default;
                vault = null;
                return false;
            }

            try
            {
                if (!vault.TryAcquireWriteLock(in handle, VaultOwnerSystemId, out buffer))
                    return false;

                lockAcquired = true;
                success =
                    !vault.IsCompactionFenceActive &&
                    buffer.IsCreated &&
                    buffer.Length >= length;
                return success;
            }
            finally
            {
                if (!success && lockAcquired && vault != null)
                    vault.ReleaseWriteLock(in handle, VaultOwnerSystemId);

                if (!success)
                {
                    buffer = default;
                    vault = null;
                }
            }
        }

        private bool TryAcquireExistingTelemetryBuffer<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int length,
            out IDataVault vault,
            out NativeArray<T> buffer)
            where T : struct
        {
            vault = null;
            buffer = default;
            vault = _dataVault;
            bool lockAcquired = false;
            bool success = false;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                !IsExactVaultHandle(in handle, bufferId))
            {
                vault = null;
                return false;
            }

            try
            {
                if (!vault.TryAcquireWriteLock(in handle, VaultOwnerSystemId, out buffer))
                    return false;

                lockAcquired = true;
                success =
                    !vault.IsCompactionFenceActive &&
                    buffer.IsCreated &&
                    buffer.Length >= length;
                return success;
            }
            finally
            {
                if (!success && lockAcquired && vault != null)
                    vault.ReleaseWriteLock(in handle, VaultOwnerSystemId);

                if (!success)
                {
                    buffer = default;
                    vault = null;
                }
            }
        }

        private bool TryReadTelemetryBuffer<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int length,
            out NativeArray<T>.ReadOnly buffer)
            where T : struct
        {
            buffer = default;
            IDataVault vault = _dataVault;
            return vault != null &&
                   !vault.IsCompactionFenceActive &&
                   IsExactVaultHandle(in handle, bufferId) &&
                   vault.TryReadOnlyHandle(in handle, out buffer) &&
                   buffer.Length >= length &&
                   !vault.IsCompactionFenceActive;
        }

        private static bool IsExactVaultHandle<T>(in VaultGenerationHandle<T> handle, BufferID expectedBufferId)
            where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)expectedBufferId) && handle.Generation != 0u;
        }

        private static void ReleaseVaultHandle<T>(IDataVault vault, ref VaultGenerationHandle<T> handle)
            where T : struct
        {
            if (vault != null && handle.BufferID != 0u && handle.Generation != 0u)
                vault.ReleaseBuffer(in handle);

            handle = default;
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

            int clearGroups = CeilDividePositive(1, _clearIndirectArgsThreadGroupSizeX);
            if (clearGroups <= 0)
                return false;

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
            _cullingCompute.Dispatch(_clearIndirectArgsKernel, clearGroups, 1, 1);
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
            bool keepCullTelemetryCountersBuffer = DeferPendingScatterCullTelemetryReadbackForRelease();
            DisposeCullTelemetryReadbackData();
            ReleaseVisibleIndexBuffer(ref _visibleIndicesLod0Buffer);
            ReleaseVisibleIndexBuffer(ref _visibleIndicesLod1Buffer);
            ReleaseVisibleIndexBuffer(ref _visibleIndicesShadowBuffer);
            ReleaseFloraSnapFlagBuffer();
            ReleaseGraphicsBuffer(ref _indirectArgsLod0Buffer);
            ReleaseGraphicsBuffer(ref _indirectArgsLod1Buffer);
            ReleaseGraphicsBuffer(ref _indirectArgsShadowBuffer);
            if (keepCullTelemetryCountersBuffer)
                _cullTelemetryCountersBuffer = null;
            else
                ReleaseGraphicsBuffer(ref _cullTelemetryCountersBuffer);
            ReleaseGraphicsBuffer(ref _cullTelemetryCountersUploadBuffer);
            ReleaseDepthPyramidTexture();
            _gpuVisibleIndexCapacity = 0;
            _gpuCullingFrameIndex = 0;
            _hasFarCullingSnapshot = false;
            if (!_scatterCullTelemetryReadbackDisposeAfterCompletion)
                _scatterCullTelemetryReadbackPending = false;
            _depthPyramidWidth = 0;
            _depthPyramidHeight = 0;
            _depthPyramidMipCount = 0;
            ResetCullComputeBindingStates();
            ResetSnapComputeBindingStates();
        }

        private void EnsureCullTelemetryReadbackData()
        {
            if (_scatterCullTelemetryReadbackDisposeAfterCompletion)
                return;

            if (HasCullTelemetryReadbackData())
                return;

            DisposeCullTelemetryReadbackData();
            _cullTelemetryReadback.Data = H8Memory.Allocate<uint>(
                ScatterCullTelemetryCounterCount,
                VaultOwnerSystemId,
                Allocator.Persistent,
                NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<uint>[4] - async GPU cull telemetry readback target - owner: HectonIndirectVegetationRenderer
            if (!_cullTelemetryReadback.Data.IsCreated)
                throw new InvalidOperationException("H8Memory allocation failed for cull telemetry readback data.");

            _scatterCullTelemetryReadbackRepairRequested = false;
        }

        private bool HasCullTelemetryReadbackData()
        {
            return _cullTelemetryReadback.Data.IsCreated &&
                   _cullTelemetryReadback.Data.Length >= ScatterCullTelemetryCounterCount;
        }

        private void QueueCullTelemetryReadbackRepair()
        {
            _scatterCullTelemetryReadbackRepairRequested = true;
        }

        private void FlushCullTelemetryReadbackRepairSlow()
        {
            if (_scatterCullTelemetryReadbackDisposeAfterCompletion)
                return;

            if (!_scatterCullTelemetryReadbackRepairRequested && HasCullTelemetryReadbackData())
                return;

            if (_cullTelemetryCountersBuffer == null || _scatterCullTelemetryReadbackPending)
                return;

            EnsureCullTelemetryReadbackData();
        }

        private Action<AsyncGPUReadbackRequest> ResolveScatterCullTelemetryReadbackCompletion()
        {
            if (_scatterCullTelemetryReadbackCompletion == null)
                _scatterCullTelemetryReadbackCompletion = OnScatterCullTelemetryReadbackComplete;

            return _scatterCullTelemetryReadbackCompletion;
        }

        private void OnScatterCullTelemetryReadbackComplete(AsyncGPUReadbackRequest request)
        {
            if (!_scatterCullTelemetryReadbackDisposeAfterCompletion)
                return;

            _scatterCullTelemetryReadbackPending = false;
            _cullTelemetryReadbackRequest = default;
            _scatterCullTelemetryReadbackDisposeAfterCompletion = false;

            bool releaseCountersBuffer = _scatterCullTelemetryReleaseCountersBufferAfterCompletion;
            _scatterCullTelemetryReleaseCountersBufferAfterCompletion = false;
            ReleaseCullTelemetryReadbackNativeData();

            if (releaseCountersBuffer)
                ReleaseGraphicsBuffer(ref _scatterCullTelemetryHeldCountersBuffer);
            else
                _scatterCullTelemetryHeldCountersBuffer = null;
        }

        private bool DeferPendingScatterCullTelemetryReadbackForRelease()
        {
            if (!_scatterCullTelemetryReadbackPending)
                return false;

            if (_cullTelemetryReadbackRequest.done)
            {
                _scatterCullTelemetryReadbackPending = false;
                _cullTelemetryReadbackRequest = default;
                return false;
            }

            bool holdCountersBuffer = _cullTelemetryCountersBuffer != null;
            _scatterCullTelemetryReadbackDisposeAfterCompletion = true;
            _scatterCullTelemetryReleaseCountersBufferAfterCompletion = holdCountersBuffer;
            _scatterCullTelemetryHeldCountersBuffer = _cullTelemetryCountersBuffer;
            _scatterCullTelemetryReadbackPending = false;
            return holdCountersBuffer;
        }

        private void DisposeCullTelemetryReadbackData()
        {
            _scatterCullTelemetryReadbackRepairRequested = false;
            if (_scatterCullTelemetryReadbackDisposeAfterCompletion)
                return;

            ReleaseCullTelemetryReadbackNativeData();
        }

        private void ReleaseCullTelemetryReadbackNativeData()
        {
            if (_cullTelemetryReadback.Data.IsCreated)
                H8Memory.Release(ref _cullTelemetryReadback.Data, VaultOwnerSystemId);
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

        private void EnsureRequiredIndirectPropertyBlocks()
        {
            if (!_preferGpuIndirectRendering || !_supportsComputeShadersCold || _cullingCompute == null)
                return;

            EnsureIndirectPropertyBlock(ref _nearIndirectProperties);

            bool hasFarPass = _farLodDistance > _nearLodDistance && ResolveImpostorRenderMesh() != null;
            if (hasFarPass)
                EnsureIndirectPropertyBlock(ref _farIndirectProperties);

            if (_enableDepthPrepass && _depthOnlyMaterial != null)
            {
                EnsureIndirectPropertyBlock(ref _depthNearIndirectProperties);
                if (hasFarPass)
                    EnsureIndirectPropertyBlock(ref _depthFarIndirectProperties);
            }

            if (_enableShadowCasterDraw && _shadowCasterMaterial != null)
                EnsureIndirectPropertyBlock(ref _shadowIndirectProperties);

            if (_enableMotionVectorDraw && _motionVectorMaterial != null)
            {
                EnsureIndirectPropertyBlock(ref _motionNearIndirectProperties);
                if (hasFarPass)
                    EnsureIndirectPropertyBlock(ref _motionFarIndirectProperties);
            }
        }

        private static void EnsureIndirectPropertyBlock(ref MaterialPropertyBlock propertyBlock)
        {
            if (propertyBlock == null)
                propertyBlock = new MaterialPropertyBlock(); // COLD ALLOC: MaterialPropertyBlock[1] - required GPU indirect pass payload - owner: HectonIndirectVegetationRenderer
        }

        private bool HasRequiredIndirectPropertyBlocks(
            bool useFarPass,
            bool useDepthPass,
            bool useDepthFarPass,
            bool useShadowPass,
            bool useMotionPass,
            bool useMotionFarPass)
        {
            return _nearIndirectProperties != null &&
                   (!useFarPass || _farIndirectProperties != null) &&
                   (!useDepthPass || _depthNearIndirectProperties != null) &&
                   (!useDepthFarPass || _depthFarIndirectProperties != null) &&
                   (!useShadowPass || _shadowIndirectProperties != null) &&
                   (!useMotionPass || _motionNearIndirectProperties != null) &&
                   (!useMotionFarPass || _motionFarIndirectProperties != null);
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

            int nextCapacity = Mathf.NextPowerOfTwo(Mathf.Max(16, instanceCount));
            EnsureVaultStorage(ref _cpuCullingMatricesHandle, CpuCullingMatricesBufferId, nextCapacity, NativeArrayOptions.UninitializedMemory);
            EnsureVaultStorage(ref _cpuCullingDataHandle, CpuCullingDataBufferId, nextCapacity, NativeArrayOptions.UninitializedMemory);
        }

        private void ReleaseCpuCullingData()
        {
            ReleaseVaultHandle(_dataVault, ref _cpuCullingMatricesHandle);
            ReleaseVaultHandle(_dataVault, ref _cpuCullingDataHandle);
            _hasCpuCullingData = false;
        }

        private void MarkCpuCullingPayloadUnavailable()
        {
            _hasCpuCullingData = false;
            _floraAgesAuthoredExternally = false;
            _floraAgeBufferDirty = true;
        }

        private void CopyCpuCullingPayload(
            Matrix4x4[] instanceMatrices,
            HectonVegetationInstanceData[] instanceData,
            int instanceCount)
        {
            if (instanceMatrices == null || instanceCount <= 0 || instanceMatrices.Length < instanceCount)
            {
                MarkCpuCullingPayloadUnavailable();
                return;
            }

            EnsureCpuCullingCapacity(instanceCount);
            EnsureFloraAgeCapacity(instanceCount);
            MarkCpuCullingPayloadUnavailable();

            if (!TryAcquireCpuCullingMatricesForWrite(instanceCount, out IDataVault matrixVault, out NativeArray<Matrix4x4> cpuMatrices))
            {
                MarkCpuCullingPayloadUnavailable();
                return;
            }

            int matrixCount = 0;
            try
            {
                matrixCount = math.min(instanceCount, cpuMatrices.Length);
                for (int instanceIndex = 0; instanceIndex < matrixCount; instanceIndex++)
                    cpuMatrices[instanceIndex] = instanceMatrices[instanceIndex];
            }
            finally
            {
                matrixVault.ReleaseWriteLock(in _cpuCullingMatricesHandle, VaultOwnerSystemId);
            }

            if (matrixCount != instanceCount ||
                !TryAcquireCpuCullingInstanceDataForWrite(instanceCount, out IDataVault dataVault, out NativeArray<HectonVegetationInstanceData> cpuData))
            {
                MarkCpuCullingPayloadUnavailable();
                return;
            }

            HectonVegetationInstanceData fallbackPayload = CreateLegacyDefaultPayload();
            int dataCount = 0;
            try
            {
                dataCount = math.min(instanceCount, cpuData.Length);
                for (int instanceIndex = 0; instanceIndex < dataCount; instanceIndex++)
                {
                    cpuData[instanceIndex] = instanceData != null && instanceData.Length > instanceIndex
                        ? instanceData[instanceIndex]
                        : fallbackPayload;
                }
            }
            finally
            {
                dataVault.ReleaseWriteLock(in _cpuCullingDataHandle, VaultOwnerSystemId);
            }

            if (dataCount != instanceCount ||
                !TryAcquireFloraAgesForWrite(instanceCount, out IDataVault floraVault, out NativeArray<float> floraAges))
            {
                MarkCpuCullingPayloadUnavailable();
                return;
            }

            int floraCount = 0;
            try
            {
                floraCount = math.min(instanceCount, floraAges.Length);
                for (int instanceIndex = 0; instanceIndex < floraCount; instanceIndex++)
                {
                    HectonVegetationInstanceData metadata = instanceData != null && instanceData.Length > instanceIndex
                        ? instanceData[instanceIndex]
                        : fallbackPayload;
                    floraAges[instanceIndex] = ResolveFloraAgeFromMetadata(metadata);
                }
            }
            finally
            {
                floraVault.ReleaseWriteLock(in _floraAges01Handle, VaultOwnerSystemId);
            }

            _hasCpuCullingData = floraCount == instanceCount;
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
                MarkCpuCullingPayloadUnavailable();
                return;
            }

            EnsureCpuCullingCapacity(instanceCount);
            EnsureFloraAgeCapacity(instanceCount);
            MarkCpuCullingPayloadUnavailable();

            if (!TryAcquireCpuCullingMatricesForWrite(instanceCount, out IDataVault matrixVault, out NativeArray<Matrix4x4> cpuMatrices))
            {
                MarkCpuCullingPayloadUnavailable();
                return;
            }

            int matrixCount = 0;
            try
            {
                matrixCount = math.min(instanceCount, math.min(instanceMatrices.Length, cpuMatrices.Length));
                NativeArray<Matrix4x4>.Copy(instanceMatrices, cpuMatrices, matrixCount);
            }
            finally
            {
                matrixVault.ReleaseWriteLock(in _cpuCullingMatricesHandle, VaultOwnerSystemId);
            }

            if (matrixCount != instanceCount ||
                !TryAcquireCpuCullingInstanceDataForWrite(instanceCount, out IDataVault dataVault, out NativeArray<HectonVegetationInstanceData> cpuData))
            {
                MarkCpuCullingPayloadUnavailable();
                return;
            }

            int dataCount = 0;
            try
            {
                dataCount = math.min(instanceCount, math.min(instanceData.Length, cpuData.Length));
                NativeArray<HectonVegetationInstanceData>.Copy(instanceData, cpuData, dataCount);
            }
            finally
            {
                dataVault.ReleaseWriteLock(in _cpuCullingDataHandle, VaultOwnerSystemId);
            }

            if (dataCount != instanceCount ||
                !TryAcquireFloraAgesForWrite(instanceCount, out IDataVault floraVault, out NativeArray<float> floraAges))
            {
                MarkCpuCullingPayloadUnavailable();
                return;
            }

            int floraCount = 0;
            try
            {
                floraCount = math.min(instanceCount, math.min(instanceData.Length, floraAges.Length));
                for (int instanceIndex = 0; instanceIndex < floraCount; instanceIndex++)
                    floraAges[instanceIndex] = ResolveFloraAgeFromMetadata(instanceData[instanceIndex]);
            }
            finally
            {
                floraVault.ReleaseWriteLock(in _floraAges01Handle, VaultOwnerSystemId);
            }

            _hasCpuCullingData = floraCount == instanceCount;
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
                    _cachedFloorBiolumStrength,
                    _cachedOceanBiolumStrength));
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
            NativeArray<Matrix4x4>.ReadOnly cpuCullingMatrices = default;
            NativeArray<HectonVegetationInstanceData>.ReadOnly cpuCullingData = default;
            bool enableCpuCulling = _hasCpuCullingData &&
                                    TryReadCpuCullingData(
                                        _instanceCount,
                                        out cpuCullingMatrices,
                                        out cpuCullingData);
            float brgLodDistanceScalar = ResolveBrgLodDistanceScalar();
            float lodTransition = Mathf.Max(_lodTransitionRange * brgLodDistanceScalar, 0.01f);
            float nearLodDistance = Mathf.Max(_nearLodDistance * brgLodDistanceScalar, 0.01f);
            float farLodDistance = Mathf.Max(nearLodDistance, _farLodDistance * brgLodDistanceScalar);
            float lod0MaxDistance = nearLodDistance + lodTransition;
            float lod1MinDistance = Mathf.Max(0f, nearLodDistance - lodTransition);
            float lod1MaxDistance = farLodDistance + lodTransition;
            Vector4 floatingOffset = ResolveVegetationFloatingOffset();
            float densityKeepProbability01 = ResolveDensityKeepProbability01();
            int densityDecimationStep = ResolveDensityDecimationStep(densityKeepProbability01);
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

            FixedList512Bytes<float4> cullingPlanes = default;
            FixedList512Bytes<float4> headlightPositionsWs = default;
            FixedList512Bytes<float4> headlightDirectionsWs = default;
            FixedList512Bytes<float4> headlightColors = default;
            FixedList512Bytes<float4> headlightConeData = default;
            bool bypassDarknessCulling = !_enableDarknessCulling;
            int cullingPlaneCount = 0;
            int headlightCount = 0;

            if (enableCpuCulling)
            {
                int planeCount = cullingContext.cullingPlanes.IsCreated ? cullingContext.cullingPlanes.Length : 0;
                if (planeCount > 0)
                {
                    int safePlaneCount = math.min(planeCount, CpuCullingScratchPlaneCapacity);
                    for (int planeIndex = 0; planeIndex < safePlaneCount; planeIndex++)
                    {
                        Plane plane = cullingContext.cullingPlanes[planeIndex];
                        cullingPlanes.Add(new float4(plane.normal.x, plane.normal.y, plane.normal.z, plane.distance));
                    }
                    cullingPlaneCount = safePlaneCount;
                }

                if (_enableDarknessCulling)
                {
                    float globalBiolum = Mathf.Max(
                        ResolveBiolumIntensityScalar(),
                        Mathf.Max(
                            _cachedFloorBiolumStrength,
                            _cachedOceanBiolumStrength));
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
                                headlightPositionsWs.Add(new float4(lightPosition.x, lightPosition.y, lightPosition.z, lightPosition.w));
                                headlightDirectionsWs.Add(new float4(lightDirection.x, lightDirection.y, lightDirection.z, lightDirection.w));
                                headlightColors.Add(new float4(lightColor.x, lightColor.y, lightColor.z, lightColor.w));
                                headlightConeData.Add(new float4(coneData.x, coneData.y, coneData.z, coneData.w));
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
                int farScratchOffset = _instanceCount;
                int shadowScratchOffset = _instanceCount + (useFarPass ? _instanceCount : 0);

                JobHandle visibilityHandle = new BuildVegetationVisibilitySlotsJob
                {
                    Matrices = cpuCullingMatrices,
                    InstanceData = cpuCullingData,
                    CullingPlanes = cullingPlanes,
                    HeadlightPositionsWs = headlightPositionsWs,
                    HeadlightDirectionsWs = headlightDirectionsWs,
                    HeadlightColors = headlightColors,
                    HeadlightConeData = headlightConeData,
                    VisibleInstances = output.visibleInstances,
                    InstanceCount = _instanceCount,
                    FarScratchOffset = farScratchOffset,
                    ShadowScratchOffset = shadowScratchOffset,
                    CullingPlaneCount = cullingPlaneCount,
                    HeadlightCount = headlightCount,
                    EnableCpuCullingFlag = enableCpuCulling ? (byte)1 : (byte)0,
                    UseFarPassFlag = useFarPass ? (byte)1 : (byte)0,
                    UseShadowPassFlag = useShadowPass ? (byte)1 : (byte)0,
                    BypassDarknessCullingFlag = bypassDarknessCulling ? (byte)1 : (byte)0,
                    DensityDecimationStep = densityDecimationStep,
                    DensityKeepProbability01 = densityKeepProbability01,
                    ViewPosition = _cachedCullCameraPosition,
                    GlobalOffset = new float3(floatingOffset.x, floatingOffset.y, floatingOffset.z),
                    Lod0MaxDistanceSq = lod0MaxDistance * lod0MaxDistance,
                    Lod1MinDistanceSq = lod1MinDistance * lod1MinDistance,
                    Lod1MaxDistanceSq = lod1MaxDistance * lod1MaxDistance
                }.Schedule(_instanceCount, 64);

                JobHandle finalizeHandle = new FinalizeVegetationDrawOutputJob
                {
                    InstanceCount = _instanceCount,
                    FarScratchOffset = farScratchOffset,
                    ShadowScratchOffset = shadowScratchOffset,
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

                bool uploadSucceeded = true;
                if (CanReuseNativeUpload(in readBuffer))
                {
                    SetInstanceCount(readBuffer.InstanceCount);
                    _lastNativeUploadAvoidedBytes = EstimateNativeUploadBytes(readBuffer.InstanceCount);
                    _lastNativeUploadBytes = 0L;
                }
                else
                {
                    uploadSucceeded = BindInstanceNativeReadBuffer(in readBuffer);
                }

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
            InvalidateNativeUploadCache();
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
                ResolvePlayerToolManagerFromCachedContext();

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

        private void ResolvePlayerToolManagerFromCachedContext()
        {
            if (_playerToolManager != null)
                return;

            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
            if (playerContext != null && playerContext.ToolManager != null)
                _playerToolManager = playerContext.ToolManager;
        }

        private static Vector4 ResolveVegetationFloatingOffset()
        {
            Vector3 totalOffset = HectonMapMagicVegetationBridge.GlobalTotalUniverseOffset;
            return new Vector4(totalOffset.x, totalOffset.y, totalOffset.z, 0f);
        }

        private GraphicsBuffer ResolveActiveInstanceDataBuffer()
        {
            if (_instanceDataBuffer != null)
                return IsValidBuffer(_instanceDataBuffer) ? _instanceDataBuffer : null;

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

        private GraphicsBuffer TryResolveActiveInstanceDataBufferHot()
        {
            if (_instanceDataBuffer != null)
                return IsValidBuffer(_instanceDataBuffer) ? _instanceDataBuffer : null;

            if (_instanceCount <= 0 ||
                _legacyDataDirty ||
                _legacyInstanceData == null ||
                !IsValidBuffer(_legacyInstanceDataBuffer) ||
                _legacyInstanceData.Length < _instanceCount ||
                _legacyInstanceDataBuffer.count < _instanceCount)
            {
                return null;
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

            if (_uploadedInstanceMatrixBufferA == null ||
                _uploadedInstanceMatrixBufferB == null ||
                _uploadedInstanceMatrixBufferA.count < instanceCount ||
                _uploadedInstanceMatrixBufferB.count < instanceCount)
            {
                if (IsUploadedMatrixBuffer(_instanceMatrixBuffer))
                {
                    InvalidateRenderStateForBufferIdentityChange(null, _instanceDataBuffer == _uploadedInstanceDataBuffer ? null : _instanceDataBuffer, _floraPhaseSeedBuffer);
                    _instanceMatrixBuffer = null;
                }

                ReleaseBuffer(ref _uploadedInstanceMatrixBufferA);
                ReleaseBuffer(ref _uploadedInstanceMatrixBufferB);

                int nextCapacity = Mathf.NextPowerOfTwo(Mathf.Max(16, instanceCount));
                // COLD ALLOC: GraphicsBuffer[nextCapacity] A - owned matrix upload staging front/back buffer - owner: HectonIndirectVegetationRenderer
                _uploadedInstanceMatrixBufferA = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<Matrix4x4>(nextCapacity);
                // COLD ALLOC: GraphicsBuffer[nextCapacity] B - owned matrix upload staging front/back buffer - owner: HectonIndirectVegetationRenderer
                _uploadedInstanceMatrixBufferB = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<Matrix4x4>(nextCapacity);
                _uploadedInstanceMatrixBuffer = null;
                _uploadedInstanceWriteBufferIndex = 0;
                InvalidateNativeUploadCache();
            }

            if (!requiresInstanceDataBuffer)
            {
                EnsureUploadedDirtyPageCapacity(instanceCount);
                return;
            }

            if (_uploadedInstanceDataBufferA == null ||
                _uploadedInstanceDataBufferB == null ||
                _uploadedInstanceDataBufferA.count < instanceCount ||
                _uploadedInstanceDataBufferB.count < instanceCount)
            {
                if (IsUploadedDataBuffer(_instanceDataBuffer))
                {
                    InvalidateRenderStateForBufferIdentityChange(IsUploadedMatrixBuffer(_instanceMatrixBuffer) ? null : _instanceMatrixBuffer, null, _floraPhaseSeedBuffer);
                    _instanceDataBuffer = null;
                }

                ReleaseBuffer(ref _uploadedInstanceDataBufferA);
                ReleaseBuffer(ref _uploadedInstanceDataBufferB);

                int nextCapacity = Mathf.NextPowerOfTwo(Mathf.Max(16, instanceCount));
                // COLD ALLOC: GraphicsBuffer[nextCapacity] A - owned metadata upload staging front/back buffer - owner: HectonIndirectVegetationRenderer
                _uploadedInstanceDataBufferA = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<HectonVegetationInstanceData>(nextCapacity);
                // COLD ALLOC: GraphicsBuffer[nextCapacity] B - owned metadata upload staging front/back buffer - owner: HectonIndirectVegetationRenderer
                _uploadedInstanceDataBufferB = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<HectonVegetationInstanceData>(nextCapacity);
                _uploadedInstanceDataBuffer = null;
                _uploadedInstanceWriteBufferIndex = 0;
                InvalidateNativeUploadCache();
            }

            EnsureUploadedDirtyPageCapacity(instanceCount);
        }

        private bool EnsureUploadedDirtyPageCapacity(int instanceCount)
        {
            int requiredPages = GraphicsBufferUploadUtility.ResolveDirtyPageCount(instanceCount, NativeUploadDirtyPageSize);
            if (requiredPages <= 0)
                return false;

            if (HasUploadedDirtyPageStorage(requiredPages))
                return EnsureUploadedDirtyPageSnapshotCapacity(requiredPages);

            ReleaseUploadedDirtyPages();
            int nextCapacity = Mathf.NextPowerOfTwo(Mathf.Max(1, requiredPages));
            bool ready =
                EnsureVaultStorage(ref _uploadedMatrixDirtyPagesAHandle, NativeUploadMatrixDirtyPagesAId, nextCapacity, NativeArrayOptions.ClearMemory) &&
                EnsureVaultStorage(ref _uploadedMatrixDirtyPagesBHandle, NativeUploadMatrixDirtyPagesBId, nextCapacity, NativeArrayOptions.ClearMemory) &&
                EnsureVaultStorage(ref _uploadedDataDirtyPagesAHandle, NativeUploadDataDirtyPagesAId, nextCapacity, NativeArrayOptions.ClearMemory) &&
                EnsureVaultStorage(ref _uploadedDataDirtyPagesBHandle, NativeUploadDataDirtyPagesBId, nextCapacity, NativeArrayOptions.ClearMemory) &&
                EnsureUploadedDirtyPageSnapshotCapacity(nextCapacity);
            if (!ready)
            {
                ReleaseUploadedDirtyPages();
                return false;
            }

            _uploadedDirtyPageCapacity = nextCapacity;
            return true;
        }

        private bool EnsureUploadedDirtyPageSnapshotCapacity(int requiredPages)
        {
            if (requiredPages <= 0)
                return false;

            if (_uploadedDirtyPageSnapshot != null && _uploadedDirtyPageSnapshotCapacity >= requiredPages)
                return true;

            int nextCapacity = Mathf.NextPowerOfTwo(Mathf.Max(1, requiredPages));
            // COLD ALLOC: byte[nextCapacity] - dirty-page upload snapshot copied under DataVault lock and consumed after release - owner: HectonIndirectVegetationRenderer
            _uploadedDirtyPageSnapshot = new byte[nextCapacity];
            _uploadedDirtyPageSnapshotCapacity = nextCapacity;
            return true;
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
            ReleaseBuffer(ref _uploadedInstanceMatrixBufferA);
            ReleaseBuffer(ref _uploadedInstanceMatrixBufferB);

            ReleaseBuffer(ref _uploadedInstanceDataBufferA);
            ReleaseBuffer(ref _uploadedInstanceDataBufferB);
            ReleaseUploadedDirtyPages();
            _uploadedInstanceMatrixBuffer = null;
            _uploadedInstanceDataBuffer = null;
            _uploadedInstanceWriteBufferIndex = 0;
            InvalidateNativeUploadCache();
        }

        private void ReleaseUploadedDirtyPages()
        {
            IDataVault vault = _dataVault;
            ReleaseVaultHandle(vault, ref _uploadedMatrixDirtyPagesAHandle);
            ReleaseVaultHandle(vault, ref _uploadedMatrixDirtyPagesBHandle);
            ReleaseVaultHandle(vault, ref _uploadedDataDirtyPagesAHandle);
            ReleaseVaultHandle(vault, ref _uploadedDataDirtyPagesBHandle);
            _uploadedDirtyPageCapacity = 0;
            _uploadedDirtyPageSnapshot = null;
            _uploadedDirtyPageSnapshotCapacity = 0;
        }

        private GraphicsBuffer ResolveUploadedMatrixWriteBuffer()
        {
            return _uploadedInstanceWriteBufferIndex == 0 ? _uploadedInstanceMatrixBufferA : _uploadedInstanceMatrixBufferB;
        }

        private GraphicsBuffer ResolveUploadedMatrixMirrorBuffer()
        {
            return _uploadedInstanceWriteBufferIndex == 0 ? _uploadedInstanceMatrixBufferB : _uploadedInstanceMatrixBufferA;
        }

        private GraphicsBuffer ResolveUploadedDataWriteBuffer()
        {
            return _uploadedInstanceWriteBufferIndex == 0 ? _uploadedInstanceDataBufferA : _uploadedInstanceDataBufferB;
        }

        private GraphicsBuffer ResolveUploadedDataMirrorBuffer()
        {
            return _uploadedInstanceWriteBufferIndex == 0 ? _uploadedInstanceDataBufferB : _uploadedInstanceDataBufferA;
        }

        private bool IsUploadedMatrixBuffer(GraphicsBuffer buffer)
        {
            return buffer != null && (buffer == _uploadedInstanceMatrixBufferA || buffer == _uploadedInstanceMatrixBufferB);
        }

        private bool IsUploadedDataBuffer(GraphicsBuffer buffer)
        {
            return buffer != null && (buffer == _uploadedInstanceDataBufferA || buffer == _uploadedInstanceDataBufferB);
        }

        private void AdvanceUploadedWriteBuffer()
        {
            _uploadedInstanceWriteBufferIndex ^= 1;
        }

        private void ClearUploadedDirtyPages(int instanceCount)
        {
            int requiredPages = GraphicsBufferUploadUtility.ResolveDirtyPageCount(instanceCount, NativeUploadDirtyPageSize);
            if (!HasUploadedDirtyPageStorage(requiredPages))
                return;

            TryClearUploadedDirtyPages(ref _uploadedMatrixDirtyPagesAHandle, NativeUploadMatrixDirtyPagesAId, instanceCount);
            TryClearUploadedDirtyPages(ref _uploadedMatrixDirtyPagesBHandle, NativeUploadMatrixDirtyPagesBId, instanceCount);
            TryClearUploadedDirtyPages(ref _uploadedDataDirtyPagesAHandle, NativeUploadDataDirtyPagesAId, instanceCount);
            TryClearUploadedDirtyPages(ref _uploadedDataDirtyPagesBHandle, NativeUploadDataDirtyPagesBId, instanceCount);
        }

        private bool HasUploadedDirtyPageStorage(int requiredPages)
        {
            return requiredPages > 0 &&
                   _uploadedDirtyPageCapacity >= requiredPages &&
                   IsExactVaultHandle(in _uploadedMatrixDirtyPagesAHandle, NativeUploadMatrixDirtyPagesAId) &&
                   IsExactVaultHandle(in _uploadedMatrixDirtyPagesBHandle, NativeUploadMatrixDirtyPagesBId) &&
                   IsExactVaultHandle(in _uploadedDataDirtyPagesAHandle, NativeUploadDataDirtyPagesAId) &&
                   IsExactVaultHandle(in _uploadedDataDirtyPagesBHandle, NativeUploadDataDirtyPagesBId);
        }

        private bool HasUploadedWriteDirtyPageBacklog(int instanceCount)
        {
            int requiredPages = GraphicsBufferUploadUtility.ResolveDirtyPageCount(instanceCount, NativeUploadDirtyPageSize);
            if (!HasUploadedDirtyPageStorage(requiredPages))
                return false;

            IDataVault vault = _dataVault;
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            VaultGenerationHandle<byte> matrixHandle = _uploadedInstanceWriteBufferIndex == 0
                ? _uploadedMatrixDirtyPagesAHandle
                : _uploadedMatrixDirtyPagesBHandle;
            VaultGenerationHandle<byte> dataHandle = _uploadedInstanceWriteBufferIndex == 0
                ? _uploadedDataDirtyPagesAHandle
                : _uploadedDataDirtyPagesBHandle;

            return HasDirtyPageBacklog(vault, in matrixHandle, requiredPages) ||
                   HasDirtyPageBacklog(vault, in dataHandle, requiredPages);
        }

        private static bool HasDirtyPageBacklog(IDataVault vault, in VaultGenerationHandle<byte> handle, int requiredPages)
        {
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                !vault.TryReadOnlyHandle(in handle, out NativeArray<byte>.ReadOnly dirtyPages) ||
                dirtyPages.Length < requiredPages)
            {
                return false;
            }

            for (int i = 0; i < requiredPages; i++)
            {
                if (dirtyPages[i] != 0)
                    return true;
            }

            return false;
        }

        private bool TryAcquireUploadedDirtyPageForWrite(
            ref VaultGenerationHandle<byte> handle,
            BufferID bufferId,
            out IDataVault vault,
            out NativeArray<byte> dirtyPages)
        {
            vault = null;
            dirtyPages = default;
            return HasUploadedDirtyPageStorage(_uploadedDirtyPageCapacity) &&
                   TryAcquireVaultStorageForWrite(
                       ref handle,
                       bufferId,
                       _uploadedDirtyPageCapacity,
                       NativeArrayOptions.ClearMemory,
                       out vault,
                       out dirtyPages);
        }

        private bool TryClearUploadedDirtyPages(
            ref VaultGenerationHandle<byte> handle,
            BufferID bufferId,
            int instanceCount)
        {
            if (!TryAcquireUploadedDirtyPageForWrite(ref handle, bufferId, out IDataVault vault, out NativeArray<byte> dirtyPages))
                return false;

            try
            {
                GraphicsBufferUploadUtility.ClearDirtyPages(dirtyPages, instanceCount, NativeUploadDirtyPageSize);
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in handle, VaultOwnerSystemId);
            }
        }

        private bool TryMarkUploadedDirtyPages(
            NativeArray<byte> sourceDirtyPages,
            ref VaultGenerationHandle<byte> targetHandle,
            BufferID targetBufferId,
            int instanceCount)
        {
            if (!TryAcquireUploadedDirtyPageForWrite(ref targetHandle, targetBufferId, out IDataVault vault, out NativeArray<byte> targetDirtyPages))
                return false;

            try
            {
                MarkUploadedBufferDirtyPages(sourceDirtyPages, targetDirtyPages, instanceCount);
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in targetHandle, VaultOwnerSystemId);
            }
        }

        private bool TryMarkAllUploadedDirtyPages(int instanceCount)
        {
            return TryMarkAllUploadedDirtyPagesForHandle(ref _uploadedMatrixDirtyPagesAHandle, NativeUploadMatrixDirtyPagesAId, instanceCount) &&
                   TryMarkAllUploadedDirtyPagesForHandle(ref _uploadedMatrixDirtyPagesBHandle, NativeUploadMatrixDirtyPagesBId, instanceCount) &&
                   TryMarkAllUploadedDirtyPagesForHandle(ref _uploadedDataDirtyPagesAHandle, NativeUploadDataDirtyPagesAId, instanceCount) &&
                   TryMarkAllUploadedDirtyPagesForHandle(ref _uploadedDataDirtyPagesBHandle, NativeUploadDataDirtyPagesBId, instanceCount);
        }

        private bool TryMarkAllUploadedDirtyPagesForHandle(
            ref VaultGenerationHandle<byte> targetHandle,
            BufferID targetBufferId,
            int instanceCount)
        {
            if (!TryAcquireUploadedDirtyPageForWrite(ref targetHandle, targetBufferId, out IDataVault vault, out NativeArray<byte> targetDirtyPages))
                return false;

            try
            {
                int requiredPages = GraphicsBufferUploadUtility.ResolveDirtyPageCount(instanceCount, NativeUploadDirtyPageSize);
                int pageCount = math.min(requiredPages, targetDirtyPages.Length);
                for (int i = 0; i < pageCount; i++)
                    targetDirtyPages[i] = 1;

                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in targetHandle, VaultOwnerSystemId);
            }
        }

        private bool TryUploadMatrixDirtyPages(
            GraphicsBuffer matrixWriteBuffer,
            NativeArray<Matrix4x4> sourceMatrices,
            int instanceCount,
            int uploadBudgetBytes,
            out bool dirty,
            out GraphicsBufferUploadUtility.PageUploadStats stats)
        {
            return _uploadedInstanceWriteBufferIndex == 0
                ? TryUploadDirtyPages(matrixWriteBuffer, sourceMatrices, ref _uploadedMatrixDirtyPagesAHandle, NativeUploadMatrixDirtyPagesAId, instanceCount, uploadBudgetBytes, out dirty, out stats)
                : TryUploadDirtyPages(matrixWriteBuffer, sourceMatrices, ref _uploadedMatrixDirtyPagesBHandle, NativeUploadMatrixDirtyPagesBId, instanceCount, uploadBudgetBytes, out dirty, out stats);
        }

        private bool TryResolveDataDirtyPageUploadState(
            int instanceCount,
            out bool dirty,
            out int firstDirtyPageBytes)
        {
            return _uploadedInstanceWriteBufferIndex == 0
                ? TryResolveDirtyPageUploadState<HectonVegetationInstanceData>(ref _uploadedDataDirtyPagesAHandle, NativeUploadDataDirtyPagesAId, instanceCount, out dirty, out firstDirtyPageBytes)
                : TryResolveDirtyPageUploadState<HectonVegetationInstanceData>(ref _uploadedDataDirtyPagesBHandle, NativeUploadDataDirtyPagesBId, instanceCount, out dirty, out firstDirtyPageBytes);
        }

        private bool TryUploadDataDirtyPages(
            GraphicsBuffer dataWriteBuffer,
            NativeArray<HectonVegetationInstanceData> sourceData,
            int instanceCount,
            int uploadBudgetBytes,
            out GraphicsBufferUploadUtility.PageUploadStats stats)
        {
            bool ignoredDirty;
            return _uploadedInstanceWriteBufferIndex == 0
                ? TryUploadDirtyPages(dataWriteBuffer, sourceData, ref _uploadedDataDirtyPagesAHandle, NativeUploadDataDirtyPagesAId, instanceCount, uploadBudgetBytes, out ignoredDirty, out stats)
                : TryUploadDirtyPages(dataWriteBuffer, sourceData, ref _uploadedDataDirtyPagesBHandle, NativeUploadDataDirtyPagesBId, instanceCount, uploadBudgetBytes, out ignoredDirty, out stats);
        }

        private bool TryResolveDirtyPageUploadState<T>(
            ref VaultGenerationHandle<byte> dirtyHandle,
            BufferID dirtyBufferId,
            int instanceCount,
            out bool dirty,
            out int firstDirtyPageBytes)
            where T : struct
        {
            dirty = false;
            firstDirtyPageBytes = 0;
            int requiredPages = GraphicsBufferUploadUtility.ResolveDirtyPageCount(instanceCount, NativeUploadDirtyPageSize);
            if (!HasUploadedDirtyPageStorage(requiredPages) ||
                !IsExactVaultHandle(in dirtyHandle, dirtyBufferId))
            {
                return false;
            }

            IDataVault vault = _dataVault;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                !vault.TryReadOnlyHandle(in dirtyHandle, out NativeArray<byte>.ReadOnly dirtyPages) ||
                dirtyPages.Length < requiredPages)
            {
                return false;
            }

            dirty = HasAnyDirtyPageReadOnly(dirtyPages, instanceCount, NativeUploadDirtyPageSize);
            firstDirtyPageBytes = dirty
                ? ResolveFirstDirtyPageBytesReadOnly<T>(dirtyPages, instanceCount, NativeUploadDirtyPageSize)
                : 0;
            return true;
        }

        private static bool HasAnyDirtyPageReadOnly(
            NativeArray<byte>.ReadOnly dirtyPages,
            int elementCount,
            int pageSize)
        {
            if (elementCount <= 0)
                return false;

            int pageCount = math.min(dirtyPages.Length, GraphicsBufferUploadUtility.ResolveDirtyPageCount(elementCount, pageSize));
            for (int i = 0; i < pageCount; i++)
            {
                if (dirtyPages[i] != 0)
                    return true;
            }

            return false;
        }

        private static int ResolveFirstDirtyPageBytesReadOnly<T>(
            NativeArray<byte>.ReadOnly dirtyPages,
            int elementCount,
            int pageSize)
            where T : struct
        {
            if (elementCount <= 0)
                return 0;

            int safePageSize = math.max(1, pageSize);
            int pageCount = math.min(dirtyPages.Length, GraphicsBufferUploadUtility.ResolveDirtyPageCount(elementCount, safePageSize));
            int stride = UnsafeUtility.SizeOf<T>();
            for (int i = 0; i < pageCount; i++)
            {
                if (dirtyPages[i] == 0)
                    continue;

                int pageElementStart = i * safePageSize;
                int pageElementCount = math.min(safePageSize, elementCount - pageElementStart);
                if (pageElementCount <= 0)
                    return 0;

                long pageBytes = (long)pageElementCount * stride;
                return pageBytes > int.MaxValue ? int.MaxValue : (int)pageBytes;
            }

            return 0;
        }

        private bool TryUploadDirtyPages<T>(
            GraphicsBuffer targetBuffer,
            NativeArray<T> sourceData,
            ref VaultGenerationHandle<byte> dirtyHandle,
            BufferID dirtyBufferId,
            int instanceCount,
            int uploadBudgetBytes,
            out bool dirty,
            out GraphicsBufferUploadUtility.PageUploadStats stats)
            where T : struct
        {
            dirty = false;
            stats = default;
            int requiredPages = GraphicsBufferUploadUtility.ResolveDirtyPageCount(instanceCount, NativeUploadDirtyPageSize);
            if (!EnsureUploadedDirtyPageSnapshotCapacity(requiredPages))
                return false;

            if (!TryAcquireUploadedDirtyPageForWrite(ref dirtyHandle, dirtyBufferId, out IDataVault vault, out NativeArray<byte> dirtyPages))
                return false;

            int copiedPageCount;
            try
            {
                copiedPageCount = CopyUploadedDirtyPagesToSnapshot(dirtyPages, requiredPages);
                dirty = HasAnyUploadedDirtyPageSnapshot(copiedPageCount);
            }
            finally
            {
                vault.ReleaseWriteLock(in dirtyHandle, VaultOwnerSystemId);
            }

            if (!dirty)
                return true;

            stats = GraphicsBufferUploadUtility.UploadNativeArrayDirtyPagesFromSnapshot(
                targetBuffer,
                sourceData,
                _uploadedDirtyPageSnapshot,
                instanceCount,
                NativeUploadDirtyPageSize,
                uploadBudgetBytes,
                markUploadedPages: true);

            if (stats.UploadedPages > 0 && !TryClearUploadedDirtyPagesFromSnapshot(ref dirtyHandle, dirtyBufferId, copiedPageCount))
                return false;

            return true;
        }

        private int CopyUploadedDirtyPagesToSnapshot(NativeArray<byte> dirtyPages, int requiredPages)
        {
            if (!dirtyPages.IsCreated || _uploadedDirtyPageSnapshot == null || requiredPages <= 0)
                return 0;

            int pageCount = math.min(requiredPages, math.min(dirtyPages.Length, _uploadedDirtyPageSnapshot.Length));
            for (int i = 0; i < pageCount; i++)
                _uploadedDirtyPageSnapshot[i] = dirtyPages[i] != 0 ? (byte)1 : (byte)0;

            return pageCount;
        }

        private bool HasAnyUploadedDirtyPageSnapshot(int pageCount)
        {
            if (_uploadedDirtyPageSnapshot == null || pageCount <= 0)
                return false;

            int limit = math.min(pageCount, _uploadedDirtyPageSnapshot.Length);
            for (int i = 0; i < limit; i++)
            {
                if (_uploadedDirtyPageSnapshot[i] != 0)
                    return true;
            }

            return false;
        }

        private bool TryClearUploadedDirtyPagesFromSnapshot(
            ref VaultGenerationHandle<byte> dirtyHandle,
            BufferID dirtyBufferId,
            int pageCount)
        {
            if (_uploadedDirtyPageSnapshot == null || pageCount <= 0)
                return true;

            if (!TryAcquireUploadedDirtyPageForWrite(ref dirtyHandle, dirtyBufferId, out IDataVault vault, out NativeArray<byte> dirtyPages))
                return false;

            try
            {
                int limit = math.min(pageCount, math.min(dirtyPages.Length, _uploadedDirtyPageSnapshot.Length));
                for (int i = 0; i < limit; i++)
                {
                    if (_uploadedDirtyPageSnapshot[i] != GraphicsBufferUploadUtility.UploadedDirtyPageSnapshotMarker)
                        continue;

                    dirtyPages[i] = 0;
                    _uploadedDirtyPageSnapshot[i] = 0;
                }

                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in dirtyHandle, VaultOwnerSystemId);
            }
        }

        private static void MarkUploadedBufferDirtyPages(
            NativeArray<byte> sourceDirtyPages,
            NativeArray<byte> targetDirtyPages,
            int instanceCount)
        {
            if (!sourceDirtyPages.IsCreated || !targetDirtyPages.IsCreated || instanceCount <= 0)
                return;

            int pageCount = math.min(
                sourceDirtyPages.Length,
                math.min(
                    targetDirtyPages.Length,
                    GraphicsBufferUploadUtility.ResolveDirtyPageCount(instanceCount, NativeUploadDirtyPageSize)));
            for (int i = 0; i < pageCount; i++)
            {
                if (sourceDirtyPages[i] == 0)
                    continue;

                targetDirtyPages[i] = 1;
            }
        }

        private int ResolveNativeUploadBudgetBytes()
        {
            float quality01 = math.saturate(math.select(1f, _cachedQualityWeight01, math.isfinite(_cachedQualityWeight01)));
            float smoothQuality01 = Smooth01(quality01);
            return Mathf.RoundToInt(math.lerp(NativeUploadMinimumBudgetBytes, NativeUploadMaximumBudgetBytes, smoothQuality01));
        }

        private static void ReleaseBuffer(ref GraphicsBuffer buffer)
        {
            if (buffer == null)
                return;

            buffer.Release();
            buffer = null;
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

            return null;
        }

        private void RefreshCullCameraCacheCold()
        {
            if (_cameraOverride != null && _cameraOverride.isActiveAndEnabled)
            {
                _cachedCullCamera = _cameraOverride;
                return;
            }

            int cameraCount = Mathf.Min(Camera.allCamerasCount, _cameraSearchCache.Length);
            if (cameraCount <= 0)
                return;

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
                    return;
                }
            }

            _cachedCullCamera = fallbackCamera;
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

            _cullFloraKernel = ResolveKernel(_cullingCompute, "CullFloraInstances");
            _cullFloraShadowKernel = ResolveKernel(_cullingCompute, "CullFloraShadowInstances");
            _clearIndirectArgsKernel = ResolveKernel(_cullingCompute, "ClearIndirectArgs");
            _clearFloraSnapFlagsKernel = ResolveKernel(_abyssalFlowFieldCompute, "ClearFloraSnapFlags");
            _flagSnappedFloraKernel = ResolveKernel(_abyssalFlowFieldCompute, "FlagSnappedFlora");
            _depthPyramidCopyKernel = ResolveKernel(_depthPyramidCompute, "CopyDepthPyramidMip0");
            _depthPyramidDownsampleKernel = ResolveKernel(_depthPyramidCompute, "DownsampleDepthPyramidMip");

            _cullFloraThreadGroupSizeX = ResolveKernelThreadGroupSizeX(_cullingCompute, _cullFloraKernel);
            _cullFloraShadowThreadGroupSizeX = ResolveKernelThreadGroupSizeX(_cullingCompute, _cullFloraShadowKernel);
            _clearIndirectArgsThreadGroupSizeX = ResolveKernelThreadGroupSizeX(_cullingCompute, _clearIndirectArgsKernel);
            _clearFloraSnapFlagsThreadGroupSizeX = ResolveKernelThreadGroupSizeX(_abyssalFlowFieldCompute, _clearFloraSnapFlagsKernel);
            _flagSnappedFloraThreadGroupSizeX = ResolveKernelThreadGroupSizeX(_abyssalFlowFieldCompute, _flagSnappedFloraKernel);
            ResolveKernelThreadGroupSizes(
                _depthPyramidCompute,
                _depthPyramidCopyKernel,
                out _depthPyramidCopyThreadGroupSizeX,
                out _depthPyramidCopyThreadGroupSizeY);
            ResolveKernelThreadGroupSizes(
                _depthPyramidCompute,
                _depthPyramidDownsampleKernel,
                out _depthPyramidDownsampleThreadGroupSizeX,
                out _depthPyramidDownsampleThreadGroupSizeY);
        }
#endif

        private void CreateAuxiliaryMaterials()
        {
            if (_indirectPropertyBlocksPrewarmAttempted)
                return;

            EnsureRequiredIndirectPropertyBlocks();
            _indirectPropertyBlocksPrewarmAttempted = true;
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

#if UNITY_EDITOR
        private static Mesh BuildImpostorCardMesh()
        {
            Mesh mesh = new Mesh
            {
                name = "HectonIndirectVegetationRenderer_ImpostorCard"
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
#endif

        private void TryRegister()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            TryRegisterLateFrameTickable();
            TryRegisterSlowTickable();
        }

        private IPlayerRuntimeContext CachePlayerContextCold()
        {
            if (_cachedPlayerContext != null)
                return _cachedPlayerContext;

            _cachedPlayerContext = GlobalRegistry.Player;
            return _cachedPlayerContext;
        }

        private void CacheRuntimeServicesCold()
        {
            if (_vramPressure == null)
                _vramPressure = GlobalRegistry.VRAMPressureReadModel;

            CacheDataVaultCold();
        }

        private void CacheGraphicsCapabilitiesCold()
        {
            _supportsComputeShadersCold = SystemInfo.supportsComputeShaders;
            _usesReversedZBufferCold = SystemInfo.usesReversedZBuffer;
        }

        private IDataVault CacheDataVaultCold()
        {
            if (_dataVault == null)
                _dataVault = GlobalRegistry.DataVault;
            return _dataVault;
        }

        private float ResolveBrgLodDistanceScalar()
        {
            float pressureScalar = 1f;
            IVramPressureReadModel pressure = _vramPressure;
            if (pressure != null)
            {
                float scalar = pressure.BrgLodDistanceScalar;
                pressureScalar = math.select(1f, math.max(0.05f, scalar), math.isfinite(scalar));
            }

            return math.max(0.05f, pressureScalar * ResolveFloraLodQualityDistanceScalar());
        }

        private float ResolveFloraLodQualityDistanceScalar()
        {
            float qualityWeight = math.saturate(math.select(1f, _cachedQualityWeight01, math.isfinite(_cachedQualityWeight01)));
            return math.lerp(0.3f, 1f, Smooth01(qualityWeight));
        }

        private void TryRegisterLateFrameTickable()
        {
            if (_isLateFrameRegistered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _isLateFrameRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void TryRegisterSlowTickable()
        {
            if (_isSlowTickRegistered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _isSlowTickRegistered = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregister()
        {
            if (_isSlowTickRegistered)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
                _isSlowTickRegistered = false;
            }

            if (_isLateFrameRegistered)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _isLateFrameRegistered = false;
            }
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

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }
    }
}
