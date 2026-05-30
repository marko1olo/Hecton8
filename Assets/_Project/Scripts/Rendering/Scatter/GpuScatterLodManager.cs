using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Core.Contracts.Signals;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.Rendering.Scatter
{
    /// <summary>
    /// Public metadata payload consumed by the indirect flora shader and the scatter DataVault seam.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = Stride)]
    public struct GpuScatterFloraInstanceData
    {
        /// <summary>GPU stride in bytes.</summary>
        public const int Stride = 64;

        /// <summary>Vegetation type flag: 0 grass, 1 kelp, 2 sargassum.</summary>
        [FieldOffset(0)]
        public float Type;

        /// <summary>Height scalar consumed by the flora material.</summary>
        [FieldOffset(4)]
        public float HeightScale;

        /// <summary>Width scalar consumed by the flora material.</summary>
        [FieldOffset(8)]
        public float WidthScale;

        /// <summary>Stable randomization seed in 0..1.</summary>
        [FieldOffset(12)]
        public float Variation;

        /// <summary>Optional template index. Negative means producer did not bind a template.</summary>
        [FieldOffset(16)]
        public float TemplateIndex;

        /// <summary>Shader runtime state lane.</summary>
        [FieldOffset(20)]
        public float RuntimeState;

        /// <summary>Packed runtime flags lane.</summary>
        [FieldOffset(24)]
        public float RuntimeFlags;

        /// <summary>Bioluminescence pulse frequency in Hertz.</summary>
        [FieldOffset(28)]
        public float PulseFrequency;

        /// <summary>Bioluminescence color and intensity payload.</summary>
        [FieldOffset(32)]
        public Vector4 BioluminescenceColor;

        /// <summary>Sway speed multiplier.</summary>
        [FieldOffset(48)]
        public float SwaySpeed;

        /// <summary>Bend amplitude multiplier.</summary>
        [FieldOffset(52)]
        public float BendAmplitude;

        /// <summary>Health lane in 0..1.</summary>
        [FieldOffset(56)]
        public float HealthNormalized;

        /// <summary>Reserved producer lane.</summary>
        [FieldOffset(60)]
        public float Reserved0;

        /// <summary>
        /// Creates a deterministic safe default for producers that only publish matrices.
        /// </summary>
        /// <param name="index">Instance index used for deterministic variation.</param>
        /// <returns>One visible, shader-safe metadata payload.</returns>
        public static GpuScatterFloraInstanceData CreateDefault(int index)
        {
            float variation = Hash01((uint)index * 747796405u + 2891336453u);
            return new GpuScatterFloraInstanceData
            {
                Type = 0f,
                HeightScale = 0.65f + variation * 0.35f,
                WidthScale = 0.8f + variation * 0.2f,
                Variation = variation,
                TemplateIndex = -1f,
                RuntimeState = 0f,
                RuntimeFlags = 0f,
                PulseFrequency = 0.35f + variation,
                BioluminescenceColor = new Vector4(0.05f, 0.35f, 0.55f, 0.15f),
                SwaySpeed = 1f,
                BendAmplitude = 1f,
                HealthNormalized = 1f,
                Reserved0 = 0f
            };
        }

        private static float Hash01(uint value)
        {
            value ^= value >> 16;
            value *= 0x7feb352du;
            value ^= value >> 15;
            value *= 0x846ca68bu;
            value ^= value >> 16;
            return (value & 0xFFFFu) * (1f / 65535f);
        }
    }

    /// <summary>
    /// Data-vault backed flora renderer that submits procedural matrices through GPU indirect drawing.
    /// </summary>
    /// <remarks>
    /// Producer contract: OSHINO or another authoring system writes matrices into
    /// <see cref="BufferID.FloraScatterMatrices"/> and optionally metadata into
    /// <see cref="BufferID.FloraScatterMetadata"/>. This renderer owns only GPU presentation buffers.
    /// </remarks>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Rendering/GPU Scatter LOD Manager")]
    public sealed unsafe class GpuScatterLodManager : MonoBehaviour,
        ILateFrameTickable,
        ISlowTickable,
        IOriginShiftListener,
        IGlobalRegistryHotSwapListener,
        IGlobalRegistryHotSwapRefListener
    {
        private const int DefaultInstanceCapacity = 100000;
        private const int DoubleBufferCount = 2;
        private const int FrustumPlaneCount = 6;
        private const int TelemetryCapacity = 300;
        private const int BurstAuditBatchSize = 64;
        private const int VisibleCountReadbackFrameStride = 60;
        private const int IndirectArgsElementCount = 5;
        private const int IndirectArgsInstanceCountIndex = 1;
        private const int IndirectArgsReadbackByteCount = sizeof(uint) * IndirectArgsElementCount;
        private const int MissingRegistryRefreshStrideFrames = 120;
        private const uint PortableMaxThreadsPerThreadGroup = 256u;
        private const int MaxDispatchGroupsPerDimension = 65535;
        private const float DefaultFallbackAspect = 1.7777778f;
        private const float CullingHysteresisMeters = 5f;
        private const float CullingHysteresisSeconds = 2f;
        private const uint BlackBoxMagic = 0x47534C4Du;
        private const uint BlackBoxVersion = 2u;
        private const uint BlackBoxFlagGpuReady = 1u << 0;
        private const uint BlackBoxFlagCameraSignal = 1u << 1;
        private const uint BlackBoxFlagStressShed = 1u << 2;
        private const uint BlackBoxFlagNonFiniteVaultMatrix = 1u << 4;
        private const uint BlackBoxFlagInvalidFrustum = 1u << 5;
        private const uint BlackBoxFlagNoActiveInstances = 1u << 6;
        private const uint BlackBoxFlagInvalidThreadGroup = 1u << 7;
        private const uint BlackBoxFlagInvalidMaterialVariant = 1u << 8;
        private const uint BlackBoxFlagNonFiniteAupShift = 1u << 9;
        private const uint BlackBoxFlagNonFiniteMetadata = 1u << 10;
        private const uint BlackBoxFlagNonFiniteAuxiliaryLane = 1u << 11;
        private const uint BlackBoxDumpReasonNonFiniteMatrix = 0x4E414E31u;
        private const uint BlackBoxDumpReasonNonFiniteMetadata = 0x4E414E32u;
        private const uint BlackBoxDumpReasonNonFiniteAuxiliaryLane = 0x4E414E33u;
        private const uint BlackBoxDumpReasonNonFiniteAup = 0x41555031u;
        private const uint BlackBoxDumpReasonAbiLayout = 0x41424931u;
        private const string GpuIndirectKeyword = "HECTON_GPU_INDIRECT";
        private const string ScatterFrameConstantsBufferName = "HectonScatterFrameConstants";
        private const int ScatterFrameConstantsStrideBytes = 176;
        private const int ScatterBlackBoxEntryStrideBytes = 64;

        private static readonly int _SourceMatricesId = Shader.PropertyToID("_HectonScatterSourceMatrices");
        private static readonly int _VisibleIndicesId = Shader.PropertyToID("_HectonScatterVisibleIndices");
        private static readonly int _VisibleMatricesId = Shader.PropertyToID("_HectonScatterVisibleMatrices");
        private static readonly int _MotionVectorsId = Shader.PropertyToID("_HectonScatterMotionVectors");
        private static readonly int _ScatterParams0Id = Shader.PropertyToID("_HectonScatterParams0");
        private static readonly int _ScatterParams1Id = Shader.PropertyToID("_HectonScatterParams1");
        private static readonly int _ScatterParams2Id = Shader.PropertyToID("_HectonScatterParams2");
        private static readonly int _ScatterParams3Id = Shader.PropertyToID("_HectonScatterParams3");
        private static readonly int _ScatterParams4Id = Shader.PropertyToID("_HectonScatterParams4");
        private static readonly int _ScatterFrustumPlane0Id = Shader.PropertyToID("_HectonScatterFrustumPlane0");
        private static readonly int _ScatterFrustumPlane1Id = Shader.PropertyToID("_HectonScatterFrustumPlane1");
        private static readonly int _ScatterFrustumPlane2Id = Shader.PropertyToID("_HectonScatterFrustumPlane2");
        private static readonly int _ScatterFrustumPlane3Id = Shader.PropertyToID("_HectonScatterFrustumPlane3");
        private static readonly int _ScatterFrustumPlane4Id = Shader.PropertyToID("_HectonScatterFrustumPlane4");
        private static readonly int _ScatterFrustumPlane5Id = Shader.PropertyToID("_HectonScatterFrustumPlane5");
        private static readonly int _ShaderInstanceMatricesId = Shader.PropertyToID("_HectonInstanceMatrices");
        private static readonly int _ShaderInstanceDataId = Shader.PropertyToID("_HectonVegetationInstanceData");
        private static readonly int _ShaderFloraAges01Id = Shader.PropertyToID("_HectonFloraAges01");
        private static readonly int _ShaderFloraPhaseSeedsId = Shader.PropertyToID("_HectonFloraPhaseSeeds");
        private static readonly int _ShaderFloraScatterVisualPayloadId = Shader.PropertyToID("_HectonFloraScatterVisualPayload");
        private static readonly int _FloraScatterVisualPayloadEnabledId = Shader.PropertyToID("_HectonFloraScatterVisualPayloadEnabled");
        private static readonly int _ShaderVisibleIndicesId = Shader.PropertyToID("_HectonVisibleInstanceIndices");
        private static readonly int _ShaderMotionVectorsId = Shader.PropertyToID("_HectonFloraMotionVectors");
        private static readonly int _GlobalFloatingOffsetId = Shader.PropertyToID("_GlobalFloatingOffset");
        private static readonly int _HectonFloatingOriginOffsetId = Shader.PropertyToID("_HectonFloatingOriginOffset");
        private static readonly int _LodNearDistanceId = Shader.PropertyToID("_HectonLodNearDistance");
        private static readonly int _LodFarDistanceId = Shader.PropertyToID("_HectonLodFarDistance");
        private static readonly int _LodTransitionRangeId = Shader.PropertyToID("_HectonLodTransitionRange");
        private static readonly int _FloraSnapFlagsEnabledId = Shader.PropertyToID("_HectonFloraSnapFlagsEnabled");
        private static readonly int _FloraFlowFieldResolutionId = Shader.PropertyToID("_HectonFloraFlowFieldResolution");
        private static readonly int _FloraInteractionCountId = Shader.PropertyToID("_HectonFloraInteractionCount");
        private static readonly int _FloraWakeCountId = Shader.PropertyToID("_HectonFloraWakeCount");
        private static readonly int _ImpactSphereCountId = Shader.PropertyToID("_HectonImpactSphereCount");
        private static readonly int _PredatorAupCountId = Shader.PropertyToID("_PredatorAUPCount");
        private static readonly int _AbyssalGridResolutionId = Shader.PropertyToID("_AbyssalGridResolution");
        private static readonly int _AbyssalFlowTextureActiveId = Shader.PropertyToID("_AbyssalFlowTextureActive");
        private static readonly int _AnisotropicSssStrengthId = Shader.PropertyToID("_AnisotropicSssStrength");
        private static readonly int _OrganicSssScaleId = Shader.PropertyToID("_OrganicSssScale");
        private static readonly int _EdgeBloomStrengthId = Shader.PropertyToID("_EdgeBloomStrength");
        private static readonly int _LocalCausticStrengthId = Shader.PropertyToID("_LocalCausticStrength");

        [Header("Runtime Assets")]
        [Tooltip("Compute shader with a ScatterCullJob kernel.")]
        [SerializeField] private ComputeShader scatterCullCompute;

        [Tooltip("Mesh submitted by Graphics.RenderMeshIndirect.")]
        [SerializeField] private Mesh floraMesh;

        [Tooltip("Material that consumes Hecton indirect vegetation buffers.")]
        [SerializeField] private Material floraMaterial;

        [Tooltip("Optional legacy low-memory material fallback. Quality scales through continuous shader constants, not keywords.")]
        [SerializeField] private Material lowTierFloraMaterial;

        [Tooltip("Optional legacy dense-material fallback. Quality scales through continuous shader constants, not keywords.")]
        [SerializeField] private Material highTierFloraMaterial;

        [Tooltip("Optional camera used for exact frustum planes. CameraFrustumSignal remains the fallback signal authority.")]
        [SerializeField] private Camera viewCamera;

        [Header("Capacity")]
        [Tooltip("Maximum flora matrices consumed from DataVault.")]
        [SerializeField, Min(1)] private int instanceCapacity = DefaultInstanceCapacity;

        [Tooltip("Active flora instance count inside the DataVault matrix buffer.")]
        [SerializeField, Min(0)] private int initialActiveInstanceCount = DefaultInstanceCapacity;

        [Header("Culling")]
        [Tooltip("Local bounds center for one flora mesh before matrix transform.")]
        [SerializeField] private Vector3 localBoundsCenter = new Vector3(0f, 0.7f, 0f);

        [Tooltip("Local bounds extents for one flora mesh before matrix transform.")]
        [SerializeField] private Vector3 localBoundsExtents = new Vector3(0.7f, 1.2f, 0.7f);

        [Tooltip("MX350/low-tier maximum flora cull distance.")]
        [SerializeField, Min(1f)] private float lowTierCullDistanceMeters = 100f;

        [Tooltip("Middle-tier maximum flora cull distance.")]
        [SerializeField, Min(1f)] private float midTierCullDistanceMeters = 250f;

        [Tooltip("High/ultra maximum flora cull distance.")]
        [SerializeField, Min(1f)] private float highTierCullDistanceMeters = 500f;

        [Tooltip("LOD crossfade width written to the flora material on high tiers.")]
        [SerializeField, Min(0f)] private float lodCrossfadeRangeMeters = 12f;

        [Tooltip("Fallback aspect ratio when the camera signal is available but no Camera component is bound.")]
        [SerializeField, Min(0.25f)] private float fallbackAspect = DefaultFallbackAspect;

        [Header("Presentation")]
        [Tooltip("Shadow policy for the indirect draw. Flora defaults to off for MX350.")]
        [SerializeField] private ShadowCastingMode shadowCastingMode = ShadowCastingMode.Off;

        [Tooltip("Whether the indirect flora draw receives shadows.")]
        [SerializeField] private bool receiveShadows;

        [Tooltip("Sway motion-vector scalar written by the culling kernel.")]
        [SerializeField, Min(0f)] private float swayMotionStrength = 0.035f;

        [Tooltip("Fallback draw bounds used until a producer publishes explicit bounds.")]
        [SerializeField] private Bounds fallbackDrawBounds = new Bounds(Vector3.zero, new Vector3(200f, 80f, 200f));

        [Header("Visual Overkill")]
        [Tooltip("Low-tier fake anisotropic subsurface strength.")]
        [SerializeField, Range(0f, 2f)] private float lowTierAnisotropicSssStrength = 0.5f;

        [Tooltip("High-tier anisotropic subsurface strength.")]
        [SerializeField, Range(0f, 2f)] private float highTierAnisotropicSssStrength = 1.15f;

        [Tooltip("Low-tier organic subsurface scale.")]
        [SerializeField, Range(0f, 4f)] private float lowTierOrganicSssScale = 0.72f;

        [Tooltip("High-tier organic subsurface scale.")]
        [SerializeField, Range(0f, 4f)] private float highTierOrganicSssScale = 1.65f;

        [Tooltip("Low-tier edge bloom kept cheap for MX350.")]
        [SerializeField, Range(0f, 2f)] private float lowTierEdgeBloomStrength = 0.28f;

        [Tooltip("High-tier edge bloom for dense translucent kelp silhouettes.")]
        [SerializeField, Range(0f, 2f)] private float highTierEdgeBloomStrength = 0.9f;

        [Tooltip("Low-tier local caustic strength.")]
        [SerializeField, Range(0f, 1f)] private float lowTierLocalCausticStrength = 0.08f;

        [Tooltip("High-tier local caustic strength.")]
        [SerializeField, Range(0f, 1f)] private float highTierLocalCausticStrength = 0.32f;

        [Header("Diagnostics")]
        [Tooltip("Optional CPU Burst audit. Off by default; RenderMeshIndirect path is GPU authoritative.")]
        [SerializeField] private bool enableBurstCullAudit;

        private GraphicsBuffer[] _matrixBuffers;
        private GraphicsBuffer[] _metadataBuffers;
        private GraphicsBuffer _visibleIndexBuffer;
        private GraphicsBuffer _visibleMatrixBuffer;
        private GraphicsBuffer _motionVectorBuffer;
        private GraphicsBuffer[] _floraAgeBuffers;
        private GraphicsBuffer[] _floraPhaseSeedBuffers;
        private GraphicsBuffer[] _floraVisualPayloadBuffers;
        private GraphicsBuffer _activeFloraAgeBuffer;
        private GraphicsBuffer _activeFloraPhaseSeedBuffer;
        private GraphicsBuffer _activeFloraVisualPayloadBuffer;
        private GraphicsBuffer _argsBuffer;
        private GraphicsBuffer _argsUploadBuffer;
        private GraphicsBuffer _frameConstantsBufferA;
        private GraphicsBuffer _frameConstantsBufferB;
        private GraphicsBuffer _activeFrameConstantsBuffer;
        private MaterialPropertyBlock _materialProperties;
        private Plane[] _cameraPlanes;
        private Vector4[] _frustumPlaneUpload;
        private readonly ScatterFrameConstants[] _frameConstantsUpload = new ScatterFrameConstants[1]; // COLD ALLOC: ScatterFrameConstants[1] - packed compute constant upload lane - owner: GpuScatterLodManager
        private readonly GraphicsBuffer.IndirectDrawIndexedArgs[] _indirectArgsUpload = new GraphicsBuffer.IndirectDrawIndexedArgs[1]; // COLD ALLOC: IndirectDrawIndexedArgs[1] - cached indirect draw args initialization upload - owner: GpuScatterLodManager
        private IDataVault _registryDataVault;
        private IDataVault _dataVault;
        private VaultGenerationHandle<Matrix4x4> _vaultMatricesHandle;
        private VaultGenerationHandle<GpuScatterFloraInstanceData> _vaultMetadataHandle;
        private VaultGenerationHandle<float> _vaultAgeHandle;
        private VaultGenerationHandle<float> _vaultPhaseSeedHandle;
        private VaultGenerationHandle<Vector4> _vaultVisualPayloadHandle;
        private VaultGenerationHandle<ScatterBlackBoxEntry> _blackBoxHandle;
        private VaultGenerationHandle<float4> _cpuFrustumPlanesHandle;
        private VaultGenerationHandle<byte> _cpuVisibilityMaskHandle;
        private Bounds _drawBounds;
        private Vector3 _aupShiftOffset;
        private Vector3 _lastCameraSignalPosition;
        private Vector3 _lastCameraSignalForward;
        private Vector3 _lastCameraSignalUp;
        private float _lastCameraSignalFovDegrees;
        private float _lastCameraSignalNearMeters;
        private float _lastCameraSignalFarMeters;
        private float _externalSystemStress01;
        private float _systemStress01;
        private float _effectiveCullDistanceMeters;
        private float _pendingCullDistanceMeters;
        private float _cullDistanceHysteresisTimer;
        private uint _lastMatrixGeneration;
        private uint _lastMetadataGeneration;
        private uint _lastAgeGeneration;
        private uint _lastPhaseSeedGeneration;
        private uint _lastVisualPayloadGeneration;
        private int _activeInstanceCount;
        private int _gpuBufferIndex;
        private int _frameConstantsUploadIndex;
        private int _scatterCullKernel = -1;
        private int _dispatchThreadGroupSizeX;
        private int _blackBoxCursor;
        private int _frameIndex;
        private int _lastVisibleFloraCount;
        private int _nextMissingRegistryRefreshFrame;
        private bool _registeredLateFrame;
        private bool _registeredSlowTick;
        private bool _hotSwapRegistered;
        private bool _originShiftListenerRegistered;
        private bool _registryRefreshRequested;
        private bool _gpuReady;
        private bool _forceUpload;
        private bool _coldSupportsComputeShaders;
        private bool _coldSupportsSetConstantBuffer;
        private bool _metadataDefaultsInitialized;
        private bool _hasMatrixGeneration;
        private bool _hasMetadataGeneration;
        private bool _hasAgeGeneration;
        private bool _hasPhaseSeedGeneration;
        private bool _hasVisualPayloadGeneration;
        private bool _hasCameraSignal;
        private bool _hasExplicitDrawBounds;
        private bool _blackBoxDumped;
        private bool _qualityCacheInitialized;
        private bool _visibleStateDirty;
        private bool _auxiliaryShaderLanesInitialized;
        private bool _visualPayloadDefaultsInitialized;
        private bool _abiLayoutValid;
        private bool _materialVariantCacheInitialized;
        private bool _cachedMaterialVariantValid;
        private float _pendingQualityWeight01 = 1f;
        private float _cachedQualityWeight01 = 1f;
        private AsyncGPUReadbackRequest _visibleCountReadbackRequest;
        private VisibleCountReadbackOwner _visibleCountReadback;
        private bool _visibleCountReadbackPending;
        private bool _visibleCountReadbackRepairRequested;
        private Mesh _boundMesh;
        private int _cachedMaterialVariantInstanceId;

        private struct VisibleCountReadbackOwner
        {
            public NativeArray<uint> Data;
        }
        private uint _boundIndexCount;
        private uint _boundStartIndex;
        private uint _boundBaseVertex;

        /// <summary>Most recent asynchronous GPU visible-count sample.</summary>
        public int LastVisibleFloraCount => _lastVisibleFloraCount;

        /// <summary>Current active source matrix count read from the DataVault handoff.</summary>
        public int ActiveInstanceCount => _activeInstanceCount;

        /// <summary>
        /// Updates the active matrix count after the producer has written into DataVault.
        /// </summary>
        /// <param name="instanceCount">Active matrix count.</param>
        /// <param name="drawBounds">World/runtime draw bounds covering the submitted flora.</param>
        public void PublishVaultInstanceRange(int instanceCount, Bounds drawBounds)
        {
            _activeInstanceCount = math.clamp(instanceCount, 0, math.max(1, instanceCapacity));
            if (IsFiniteBounds(drawBounds))
            {
                _drawBounds = drawBounds;
                _hasExplicitDrawBounds = true;
            }

            _forceUpload = true;
        }

        /// <summary>
        /// Marks the DataVault matrix/metadata buffers dirty without coupling this renderer to the producer type.
        /// </summary>
        /// <param name="instanceCount">Active matrix count after the producer write.</param>
        public void MarkVaultDirty(int instanceCount)
        {
            _activeInstanceCount = math.clamp(instanceCount, 0, math.max(1, instanceCapacity));
            _forceUpload = true;
        }

        /// <summary>
        /// Supplies external system stress for homeostasis-driven flora shedding.
        /// </summary>
        /// <param name="systemStress01">Stress in 0..1. Values above 0.8 halve the active cull distance.</param>
        public void SetSystemStress01(float systemStress01)
        {
            _externalSystemStress01 = Sanitize01(systemStress01);
        }

        /// <summary>
        /// Runs the optional CPU Burst frustum audit once for diagnostics.
        /// </summary>
        /// <returns>True when an audit job was scheduled and completed.</returns>
        public bool RunBurstCullAuditOnce()
        {
            if (_activeInstanceCount <= 0 || !EnsureCpuAuditBuffers(_activeInstanceCount))
                return false;

            if (!TryResolveMatrixView(out var matrices) ||
                !TryResolveCpuFrustumPlaneView(out var frustumPlanes) ||
                !TryResolveCpuVisibilityMaskView(out var visibilityMask))
            {
                return false;
            }

            UploadCpuFrustumPlanes();
            Vector3 safeLocalBoundsCenter = ResolveSafeLocalBoundsCenter();
            Vector3 safeLocalBoundsExtents = ResolveSafeLocalBoundsExtents();
            JobHandle handle = new ScatterCullJob
            {
                Matrices = matrices,
                CullingPlanes = frustumPlanes,
                VisibilityMask = visibilityMask,
                InstanceCount = _activeInstanceCount,
                AupShiftOffset = new float3(_aupShiftOffset.x, _aupShiftOffset.y, _aupShiftOffset.z),
                CameraPosition = new float3(_lastCameraSignalPosition.x, _lastCameraSignalPosition.y, _lastCameraSignalPosition.z),
                LocalBoundsCenter = new float3(safeLocalBoundsCenter.x, safeLocalBoundsCenter.y, safeLocalBoundsCenter.z),
                LocalBoundsExtents = new float3(safeLocalBoundsExtents.x, safeLocalBoundsExtents.y, safeLocalBoundsExtents.z),
                MaxDistanceSq = _effectiveCullDistanceMeters * _effectiveCullDistanceMeters
            }.Schedule(_activeInstanceCount, BurstAuditBatchSize);

            DispatcherJobFence.TryComplete(ref handle, forceComplete: true); // COLD SYNC JOB: explicit manual audit, never part of the shipping Tick path.
            return true;
        }

        private void Awake()
        {
            _matrixBuffers = new GraphicsBuffer[DoubleBufferCount]; // COLD ALLOC: GraphicsBuffer[2] - double-buffered matrix upload handles - owner: GpuScatterLodManager
            _metadataBuffers = new GraphicsBuffer[DoubleBufferCount]; // COLD ALLOC: GraphicsBuffer[2] - double-buffered flora metadata upload handles - owner: GpuScatterLodManager
            _floraAgeBuffers = new GraphicsBuffer[DoubleBufferCount]; // COLD ALLOC: GraphicsBuffer[2] - double-buffered flora age upload handles - owner: GpuScatterLodManager
            _floraPhaseSeedBuffers = new GraphicsBuffer[DoubleBufferCount]; // COLD ALLOC: GraphicsBuffer[2] - double-buffered flora phase upload handles - owner: GpuScatterLodManager
            _floraVisualPayloadBuffers = new GraphicsBuffer[DoubleBufferCount]; // COLD ALLOC: GraphicsBuffer[2] - double-buffered flora visual payload handles - owner: GpuScatterLodManager
            _cameraPlanes = new Plane[FrustumPlaneCount]; // COLD ALLOC: Plane[6] - camera frustum cache - owner: GpuScatterLodManager
            _frustumPlaneUpload = new Vector4[FrustumPlaneCount]; // COLD ALLOC: Vector4[6] - compute frustum upload cache - owner: GpuScatterLodManager
            _materialProperties = new MaterialPropertyBlock(); // COLD ALLOC: MaterialPropertyBlock[1] - per-draw indirect flora shader state - owner: GpuScatterLodManager
            CacheGraphicsCapabilitiesCold();
            instanceCapacity = math.max(1, instanceCapacity);
            _abiLayoutValid = ValidateAbiLayoutCold();
            if (!_abiLayoutValid)
            {
                enabled = false;
                return;
            }

            _activeInstanceCount = math.clamp(initialActiveInstanceCount, 0, instanceCapacity);
            _drawBounds = fallbackDrawBounds;
            _effectiveCullDistanceMeters = SanitizePositiveFinite(lowTierCullDistanceMeters, 100f);
            _pendingCullDistanceMeters = _effectiveCullDistanceMeters;
            RefreshAupOffsetCold();
        }

        private void CacheGraphicsCapabilitiesCold()
        {
            _coldSupportsComputeShaders = SystemInfo.supportsComputeShaders;
            _coldSupportsSetConstantBuffer = SystemInfo.supportsSetConstantBuffer;
        }

        private void OnEnable()
        {
            if (!_abiLayoutValid)
                return;

            RefreshAupOffsetCold();
            TryRegisterHotSwapListener();
            RefreshContinuousQualityPolicy(forceCommit: true);
            TryRegisterOriginShiftListener();
            TryRegisterLateFrame();
            TryRegisterSlowTick();
            _forceUpload = true;
        }

        private void OnDisable()
        {
            TryUnregisterLateFrame();
            TryUnregisterSlowTick();
            TryUnregisterOriginShiftListener();
            TryUnregisterHotSwapListener();
            ReleaseOwnedVaultHandles(_dataVault);
            ReleaseGpuBuffers();
            ReleaseCpuAuditBuffers();
            InvalidateDataVaultLease();
            _gpuReady = false;
        }

        private void OnDestroy()
        {
            ReleaseOwnedVaultHandles(_dataVault);
            ReleaseGpuBuffers();
            ReleaseCpuAuditBuffers();
            InvalidateDataVaultLease();
            _gpuReady = false;
        }

        /// <inheritdoc />
        public void LateFrameTick()
        {
            RunScatterVisualTick(math.max(0f, SystemDispatcher.CurrentFrameDeltaTime));
        }

        public void SlowTick()
        {
            if (_registryRefreshRequested || _registryDataVault == null)
            {
                RefreshCachedRegistryServices();
                _registryRefreshRequested = _registryDataVault == null;
            }

            if (!_gpuReady || !IsGpuStateValid())
                TryEnsureGpuState();

            FlushVisibleCountReadbackRepairSlow();
        }

        private void RunScatterVisualTick(float deltaTime)
        {
            if (!HasGpuStateReady())
                return;

            ConsumeCameraFrustumSignals();
            ConsumeSystemHealthSignals();
            RefreshContinuousQualityPolicy(forceCommit: false);
            UpdateCullDistance(deltaTime);
            if (!TryBuildFrustumPlanes())
            {
                ClearVisibleState();
                RecordBlackBox(BlackBoxFlagInvalidFrustum, ResolveSafeActiveCount());
                return;
            }

            int activeCount = ResolveSafeActiveCount();
            if (activeCount <= 0)
            {
                ClearVisibleState();
                RecordBlackBox(BlackBoxFlagNoActiveInstances, activeCount);
                return;
            }

            if (!TryUploadVaultBuffers(activeCount))
            {
                ClearVisibleState();
                RecordBlackBox(BlackBoxFlagNonFiniteVaultMatrix, activeCount);
                return;
            }

            if (!TryValidateRenderMaterialVariant(activeCount))
            {
                _frameIndex++;
                return;
            }

            DispatchCull(activeCount);
            UpdateVisibleCountReadback(_frameIndex);
            if (!Render(activeCount))
            {
                _frameIndex++;
                return;
            }

            RecordBlackBox(BuildRuntimeFlags(), activeCount);
            _frameIndex++;
        }

        /// <inheritdoc />
        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            if (TryToVector3(shiftData.NewTotalOffsetDouble, out Vector3 aupShiftOffset))
            {
                _aupShiftOffset = aupShiftOffset;
            }
            else
            {
                _aupShiftOffset = Vector3.zero;
                _hasExplicitDrawBounds = false;
                RecordBlackBox(BlackBoxFlagNonFiniteAupShift, ResolveSafeActiveCount());
                DumpBlackBox(BlackBoxDumpReasonNonFiniteAup);
            }

            if (_hasExplicitDrawBounds && IsFiniteVector(shiftData.ShiftOffset))
            {
                _drawBounds.center -= shiftData.ShiftOffset;
            }
            else if (_hasExplicitDrawBounds)
            {
                _hasExplicitDrawBounds = false;
                RecordBlackBox(BlackBoxFlagNonFiniteAupShift, ResolveSafeActiveCount());
                DumpBlackBox(BlackBoxDumpReasonNonFiniteAup);
            }

            _forceUpload = true;
        }

        /// <inheritdoc />
        void IGlobalRegistryHotSwapRefListener.OnGlobalRegistryServiceRebound(GlobalRegistryServiceSlot serviceSlot, ref object currentService)
        {
            ApplyRegistryServiceRebind(serviceSlot, currentService);
        }

        /// <inheritdoc />
        void IGlobalRegistryHotSwapListener.OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            ApplyRegistryServiceRebind(serviceSlot, currentService);
        }

        private void TryRegisterLateFrame()
        {
            if (_registeredLateFrame)
                return;

            _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregisterLateFrame()
        {
            if (!_registeredLateFrame)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
            _registeredLateFrame = false;
        }

        private void TryRegisterSlowTick()
        {
            if (_registeredSlowTick || !Application.isPlaying)
                return;

            _registeredSlowTick = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregisterSlowTick()
        {
            if (!_registeredSlowTick)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
            _registeredSlowTick = false;
        }

        private void TryRegisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);

            RefreshCachedRegistryServices();
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        private void TryRegisterOriginShiftListener()
        {
            if (_originShiftListenerRegistered || !Application.isPlaying)
                return;

            HectonFloatingOrigin.RegisterListener(this);
            _originShiftListenerRegistered = true;
        }

        private void TryUnregisterOriginShiftListener()
        {
            if (!_originShiftListenerRegistered)
                return;

            HectonFloatingOrigin.UnregisterListener(this);
            _originShiftListenerRegistered = false;
        }

        private void RefreshCachedRegistryServices()
        {
            ApplyRegistryServiceRebind(GlobalRegistryServiceSlot.DataVault, GlobalRegistry.DataVault);
            _nextMissingRegistryRefreshFrame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex + MissingRegistryRefreshStrideFrames;
        }

        private void RefreshMissingRegistryServicesIfNeeded()
        {
            if (_registryDataVault != null)
                return;

            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (frame < _nextMissingRegistryRefreshFrame)
                return;

            _registryRefreshRequested = true;
            _nextMissingRegistryRefreshFrame = frame + MissingRegistryRefreshStrideFrames;
        }

        private void ApplyRegistryServiceRebind(GlobalRegistryServiceSlot serviceSlot, object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.DataVault)
                return;

            IDataVault currentVault = currentService as IDataVault;
            _registryDataVault = currentVault;
            if (!ReferenceEquals(_dataVault, currentVault))
            {
                ReleaseOwnedVaultHandles(_dataVault);
                InvalidateDataVaultLease();
                _gpuReady = false;
            }
        }

        private bool TryEnsureGpuState()
        {
            if (!_abiLayoutValid)
                return false;

            if (_gpuReady && IsGpuStateValid())
                return true;

            _gpuReady = false;
            if (scatterCullCompute == null ||
                !_coldSupportsComputeShaders ||
                floraMesh == null ||
                !HasAnyConfiguredMaterial())
                return false;

            RefreshMissingRegistryServicesIfNeeded();
            IDataVault vault = _registryDataVault;
            if (vault == null || vault.IsCompactionFenceActive)
            {
                InvalidateDataVaultLease();
                return false;
            }

            if (!ReferenceEquals(_dataVault, vault))
                BindDataVault(vault);

            if (!TryResolveScatterVaultBuffer(vault, ref _vaultMatricesHandle, BufferID.FloraScatterMatrices, instanceCapacity, out NativeArray<Matrix4x4> _) ||
                !TryResolveScatterVaultBuffer(vault, ref _vaultMetadataHandle, BufferID.FloraScatterMetadata, instanceCapacity, out NativeArray<GpuScatterFloraInstanceData> _) ||
                !TryResolveScatterVaultBuffer(vault, ref _vaultAgeHandle, BufferID.FloraScatterAge01, instanceCapacity, out NativeArray<float> _) ||
                !TryResolveScatterVaultBuffer(vault, ref _vaultPhaseSeedHandle, BufferID.FloraScatterPhaseSeeds, instanceCapacity, out NativeArray<float> _) ||
                !TryResolveScatterVaultBuffer(vault, ref _vaultVisualPayloadHandle, BufferID.FloraScatterVisualPayload, instanceCapacity, out NativeArray<Vector4> _))
            {
                return false;
            }

            _scatterCullKernel = ResolveKernel(scatterCullCompute, "ScatterCullJob");
            if (_scatterCullKernel < 0)
                return false;
            if (!TryResolveDispatchThreadGroupSize())
                return false;

            EnsureGpuBuffers();
            InitializeIndirectArgs(floraMesh);
            EnsureVisibleCountReadbackData();
            _gpuReady = IsGpuStateValid();
            return _gpuReady;
        }

        private bool HasGpuStateReady()
        {
            if (!_abiLayoutValid ||
                !_gpuReady ||
                !IsGpuStateValid() ||
                scatterCullCompute == null ||
                !_coldSupportsComputeShaders ||
                floraMesh == null ||
                !HasAnyConfiguredMaterial())
            {
                _gpuReady = false;
                return false;
            }

            IDataVault vault = _dataVault;
            if (vault == null || vault.IsCompactionFenceActive)
            {
                _gpuReady = false;
                return false;
            }

            return true;
        }

        private bool IsGpuStateValid()
        {
            return _matrixBuffers != null &&
                   _metadataBuffers != null &&
                   _matrixBuffers[0] != null &&
                   _matrixBuffers[1] != null &&
                   _metadataBuffers[0] != null &&
                   _metadataBuffers[1] != null &&
                   _visibleIndexBuffer != null &&
                   _visibleMatrixBuffer != null &&
                   _motionVectorBuffer != null &&
                   _floraAgeBuffers != null &&
                   _floraAgeBuffers[0] != null &&
                   _floraAgeBuffers[1] != null &&
                   _floraPhaseSeedBuffers != null &&
                   _floraPhaseSeedBuffers[0] != null &&
                   _floraPhaseSeedBuffers[1] != null &&
                   _floraVisualPayloadBuffers != null &&
                   _floraVisualPayloadBuffers[0] != null &&
                   _floraVisualPayloadBuffers[1] != null &&
                   _argsBuffer != null &&
                   _scatterCullKernel >= 0;
        }

        private void BindDataVault(IDataVault vault)
        {
            _dataVault = vault;
            bool needsDefaultAges = !TryResolveScatterVaultBuffer(vault, ref _vaultAgeHandle, BufferID.FloraScatterAge01, instanceCapacity, out NativeArray<float> _);
            bool needsDefaultPhaseSeeds = !TryResolveScatterVaultBuffer(vault, ref _vaultPhaseSeedHandle, BufferID.FloraScatterPhaseSeeds, instanceCapacity, out NativeArray<float> _);
            bool needsDefaultVisualPayload = !TryResolveScatterVaultBuffer(vault, ref _vaultVisualPayloadHandle, BufferID.FloraScatterVisualPayload, instanceCapacity, out NativeArray<Vector4> _);

            if (!TryAcquireScatterVaultBuffer(vault, ref _vaultMatricesHandle, BufferID.FloraScatterMatrices, instanceCapacity, NativeArrayOptions.UninitializedMemory, out NativeArray<Matrix4x4> _) ||
                !TryAcquireScatterVaultBuffer(vault, ref _vaultMetadataHandle, BufferID.FloraScatterMetadata, instanceCapacity, NativeArrayOptions.ClearMemory, out NativeArray<GpuScatterFloraInstanceData> _) ||
                !TryAcquireScatterVaultBuffer(vault, ref _vaultAgeHandle, BufferID.FloraScatterAge01, instanceCapacity, NativeArrayOptions.ClearMemory, out NativeArray<float> _) ||
                !TryAcquireScatterVaultBuffer(vault, ref _vaultPhaseSeedHandle, BufferID.FloraScatterPhaseSeeds, instanceCapacity, NativeArrayOptions.ClearMemory, out NativeArray<float> _) ||
                !TryAcquireScatterVaultBuffer(vault, ref _vaultVisualPayloadHandle, BufferID.FloraScatterVisualPayload, instanceCapacity, NativeArrayOptions.ClearMemory, out NativeArray<Vector4> _))
            {
                CaptureVaultGenerations(vault);
                _forceUpload = true;
                return;
            }

            EnsureBlackBox(vault);
            CaptureVaultGenerations(vault);
            EnsureMetadataDefaults();
            EnsureAuxiliaryShaderLaneDefaults(needsDefaultAges, needsDefaultPhaseSeeds);
            EnsureVisualPayloadDefaults(needsDefaultVisualPayload);
            _forceUpload = true;
        }

        private static bool TryAcquireScatterVaultBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (vault == null || requiredLength <= 0)
                return false;

            handle = vault.EnsureGenerationHandle<T>(
                bufferId,
                requiredLength,
                SystemID.Vfx,
                options);
            return TryResolveScatterVaultBuffer(vault, ref handle, bufferId, requiredLength, out buffer);
        }

        private static bool TryResolveScatterVaultBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (vault == null || requiredLength <= 0)
                return false;

            if (IsMatchingScatterVaultHandle(in handle, bufferId) &&
                vault.TryResolveHandle(in handle, out buffer) &&
                buffer.IsCreated &&
                buffer.Length >= requiredLength)
            {
                return true;
            }

            if (!vault.TryGetGenerationHandle<T>(bufferId, out handle) ||
                !IsMatchingScatterVaultHandle(in handle, bufferId) ||
                !vault.TryResolveHandle(in handle, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                buffer = default;
                return false;
            }

            return true;
        }

        private static bool TryReadScatterVaultGeneration<T>(
            IDataVault vault,
            BufferID bufferId,
            out uint generation) where T : struct
        {
            generation = 0u;
            if (vault == null ||
                !vault.TryGetGenerationHandle<T>(bufferId, out VaultGenerationHandle<T> handle) ||
                !IsMatchingScatterVaultHandle(in handle, bufferId))
            {
                return false;
            }

            generation = handle.Generation;
            return true;
        }

        private static bool IsMatchingScatterVaultHandle<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId) where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)bufferId) &&
                   handle.SystemID == (uint)SystemID.Vfx &&
                   handle.Generation != 0u;
        }

        private void ReleaseOwnedVaultHandles(IDataVault vault)
        {
            ReleaseScatterVaultHandle(vault, ref _blackBoxHandle, BufferID.FloraScatterBlackBox);
            ReleaseScatterVaultHandle(vault, ref _cpuFrustumPlanesHandle, BufferID.FloraScatterCpuFrustumPlanes);
            ReleaseScatterVaultHandle(vault, ref _cpuVisibilityMaskHandle, BufferID.FloraScatterCpuVisibilityMask);
        }

        private static void ReleaseScatterVaultHandle<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId) where T : struct
        {
            if (vault != null && IsMatchingScatterVaultHandle(in handle, bufferId))
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private void InvalidateDataVaultLease()
        {
            _dataVault = null;
            _vaultMatricesHandle = default;
            _vaultMetadataHandle = default;
            _vaultAgeHandle = default;
            _vaultPhaseSeedHandle = default;
            _vaultVisualPayloadHandle = default;
            _blackBoxHandle = default;
            _cpuFrustumPlanesHandle = default;
            _cpuVisibilityMaskHandle = default;
            _hasMatrixGeneration = false;
            _hasMetadataGeneration = false;
            _hasAgeGeneration = false;
            _hasPhaseSeedGeneration = false;
            _hasVisualPayloadGeneration = false;
            _lastMatrixGeneration = 0u;
            _lastMetadataGeneration = 0u;
            _lastAgeGeneration = 0u;
            _lastPhaseSeedGeneration = 0u;
            _lastVisualPayloadGeneration = 0u;
            _metadataDefaultsInitialized = false;
            _auxiliaryShaderLanesInitialized = false;
            _visualPayloadDefaultsInitialized = false;
            _blackBoxCursor = 0;
            _blackBoxDumped = false;
            _forceUpload = true;
        }

        private void EnsureGpuBuffers()
        {
            if (_matrixBuffers == null)
                _matrixBuffers = new GraphicsBuffer[DoubleBufferCount];
            if (_metadataBuffers == null)
                _metadataBuffers = new GraphicsBuffer[DoubleBufferCount];
            if (_floraAgeBuffers == null)
                _floraAgeBuffers = new GraphicsBuffer[DoubleBufferCount];
            if (_floraPhaseSeedBuffers == null)
                _floraPhaseSeedBuffers = new GraphicsBuffer[DoubleBufferCount];
            if (_floraVisualPayloadBuffers == null)
                _floraVisualPayloadBuffers = new GraphicsBuffer[DoubleBufferCount];

            for (int i = 0; i < DoubleBufferCount; i++)
            {
                if (_matrixBuffers[i] == null || _matrixBuffers[i].count < instanceCapacity)
                    RecreateStructuredLockBuffer(ref _matrixBuffers[i], instanceCapacity, Matrix4x4StrideBytes);
                if (_metadataBuffers[i] == null || _metadataBuffers[i].count < instanceCapacity)
                    RecreateStructuredLockBuffer(ref _metadataBuffers[i], instanceCapacity, GpuScatterFloraInstanceData.Stride);
                if (_floraAgeBuffers[i] == null || _floraAgeBuffers[i].count < instanceCapacity)
                    RecreateStructuredLockBuffer(ref _floraAgeBuffers[i], instanceCapacity, sizeof(float));
                if (_floraPhaseSeedBuffers[i] == null || _floraPhaseSeedBuffers[i].count < instanceCapacity)
                    RecreateStructuredLockBuffer(ref _floraPhaseSeedBuffers[i], instanceCapacity, sizeof(float));
                if (_floraVisualPayloadBuffers[i] == null || _floraVisualPayloadBuffers[i].count < instanceCapacity)
                    RecreateStructuredLockBuffer(ref _floraVisualPayloadBuffers[i], instanceCapacity, UnsafeSizeOfVector4());
            }

            if (_activeFloraAgeBuffer == null || !_activeFloraAgeBuffer.IsValid())
                _activeFloraAgeBuffer = _floraAgeBuffers[0];
            if (_activeFloraPhaseSeedBuffer == null || !_activeFloraPhaseSeedBuffer.IsValid())
                _activeFloraPhaseSeedBuffer = _floraPhaseSeedBuffers[0];
            if (_activeFloraVisualPayloadBuffer == null || !_activeFloraVisualPayloadBuffer.IsValid())
                _activeFloraVisualPayloadBuffer = _floraVisualPayloadBuffers[0];

            if (_visibleIndexBuffer == null || _visibleIndexBuffer.count < instanceCapacity)
                RecreateAppendBuffer(ref _visibleIndexBuffer, instanceCapacity, sizeof(uint));
            if (_visibleMatrixBuffer == null || _visibleMatrixBuffer.count < instanceCapacity)
                RecreateAppendBuffer(ref _visibleMatrixBuffer, instanceCapacity, Matrix4x4StrideBytes);
            if (_motionVectorBuffer == null || _motionVectorBuffer.count < instanceCapacity)
                RecreateStructuredBuffer(ref _motionVectorBuffer, instanceCapacity, UnsafeSizeOfVector4());
            if (_argsBuffer == null)
            {
                _argsBuffer = new GraphicsBuffer(
                    GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Raw | GraphicsBuffer.Target.CopyDestination,
                    1,
                    GraphicsBuffer.IndirectDrawIndexedArgs.size); // COLD ALLOC: GraphicsBuffer[1] - indirect flora draw args - owner: GpuScatterLodManager
                _argsUploadBuffer = GraphicsBufferUploadUtility.CreateRawIndirectUploadStagingBuffer(
                    1,
                    GraphicsBuffer.IndirectDrawIndexedArgs.size); // COLD ALLOC: GraphicsBuffer[1] - CPU-visible indirect flora args staging, GPU copy source only - owner: GpuScatterLodManager
                InvalidateIndirectArgsCache();
            }
            else if (_argsUploadBuffer == null)
            {
                _argsUploadBuffer = GraphicsBufferUploadUtility.CreateRawIndirectUploadStagingBuffer(
                    1,
                    GraphicsBuffer.IndirectDrawIndexedArgs.size);
            }

            if (_coldSupportsSetConstantBuffer &&
                (_frameConstantsBufferA == null || !_frameConstantsBufferA.IsValid() ||
                 _frameConstantsBufferB == null || !_frameConstantsBufferB.IsValid()))
            {
                ReleaseBuffer(ref _frameConstantsBufferA);
                ReleaseBuffer(ref _frameConstantsBufferB);
                _frameConstantsBufferA = new GraphicsBuffer(
                    GraphicsBuffer.Target.Constant,
                    GraphicsBuffer.UsageFlags.LockBufferForWrite,
                    1,
                    ScatterFrameConstantsStrideBytes); // COLD ALLOC: GraphicsBuffer[176B] - packed scatter compute constants A - owner: GpuScatterLodManager
                _frameConstantsBufferB = new GraphicsBuffer(
                    GraphicsBuffer.Target.Constant,
                    GraphicsBuffer.UsageFlags.LockBufferForWrite,
                    1,
                    ScatterFrameConstantsStrideBytes); // COLD ALLOC: GraphicsBuffer[176B] - packed scatter compute constants B - owner: GpuScatterLodManager
                _activeFrameConstantsBuffer = _frameConstantsBufferA;
            }
        }

        private void RecreateStructuredLockBuffer(ref GraphicsBuffer buffer, int count, int stride)
        {
            ReleaseBuffer(ref buffer);
            buffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                count,
                stride); // COLD ALLOC: GraphicsBuffer[count] - double-buffered scatter upload - owner: GpuScatterLodManager
        }

        private void RecreateStructuredBuffer(ref GraphicsBuffer buffer, int count, int stride)
        {
            ReleaseBuffer(ref buffer);
            buffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                count,
                stride); // COLD ALLOC: GraphicsBuffer[count] - compute-written scatter data - owner: GpuScatterLodManager
        }

        private void RecreateAppendBuffer(ref GraphicsBuffer buffer, int count, int stride)
        {
            ReleaseBuffer(ref buffer);
            buffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Append,
                count,
                stride); // COLD ALLOC: GraphicsBuffer[count] - append-visible scatter stream - owner: GpuScatterLodManager
        }

        private void ReleaseGpuBuffers()
        {
            CompletePendingVisibleCountReadbackForRelease();
            DisposeVisibleCountReadbackData();

            if (_matrixBuffers != null)
            {
                for (int i = 0; i < _matrixBuffers.Length; i++)
                    ReleaseBuffer(ref _matrixBuffers[i]);
            }

            if (_metadataBuffers != null)
            {
                for (int i = 0; i < _metadataBuffers.Length; i++)
                    ReleaseBuffer(ref _metadataBuffers[i]);
            }

            ReleaseBuffer(ref _visibleIndexBuffer);
            ReleaseBuffer(ref _visibleMatrixBuffer);
            ReleaseBuffer(ref _motionVectorBuffer);
            if (_floraAgeBuffers != null)
            {
                for (int i = 0; i < _floraAgeBuffers.Length; i++)
                    ReleaseBuffer(ref _floraAgeBuffers[i]);
            }

            if (_floraPhaseSeedBuffers != null)
            {
                for (int i = 0; i < _floraPhaseSeedBuffers.Length; i++)
                    ReleaseBuffer(ref _floraPhaseSeedBuffers[i]);
            }

            if (_floraVisualPayloadBuffers != null)
            {
                for (int i = 0; i < _floraVisualPayloadBuffers.Length; i++)
                    ReleaseBuffer(ref _floraVisualPayloadBuffers[i]);
            }

            _activeFloraAgeBuffer = null;
            _activeFloraPhaseSeedBuffer = null;
            _activeFloraVisualPayloadBuffer = null;
            ReleaseBuffer(ref _argsBuffer);
            ReleaseBuffer(ref _argsUploadBuffer);
            ReleaseBuffer(ref _frameConstantsBufferA);
            ReleaseBuffer(ref _frameConstantsBufferB);
            _activeFrameConstantsBuffer = null;
            InvalidateIndirectArgsCache();
            _dispatchThreadGroupSizeX = 0;
            _visibleStateDirty = false;
            _gpuReady = false;
        }

        private static void ReleaseBuffer(ref GraphicsBuffer buffer)
        {
            if (buffer == null)
                return;

            buffer.Release();
            buffer = null;
        }

        private void InvalidateIndirectArgsCache()
        {
            _boundMesh = null;
            _boundIndexCount = 0u;
            _boundStartIndex = 0u;
            _boundBaseVertex = 0u;
        }

        private void InitializeIndirectArgs(Mesh mesh)
        {
            if (mesh == null || _argsBuffer == null)
                return;

            uint indexCount = mesh.GetIndexCount(0);
            uint startIndex = mesh.GetIndexStart(0);
            uint baseVertex = (uint)math.max(0, mesh.GetBaseVertex(0));
            if (ReferenceEquals(_boundMesh, mesh) &&
                _boundIndexCount == indexCount &&
                _boundStartIndex == startIndex &&
                _boundBaseVertex == baseVertex)
            {
                return;
            }

            _indirectArgsUpload[0] = new GraphicsBuffer.IndirectDrawIndexedArgs
            {
                indexCountPerInstance = indexCount,
                instanceCount = 0u,
                startIndex = startIndex,
                baseVertexIndex = baseVertex,
                startInstance = 0u
            };
            GraphicsBufferUploadUtility.UploadArrayAndCopyWholeBuffer(_argsUploadBuffer, _argsBuffer, _indirectArgsUpload, 1);

            _boundMesh = mesh;
            _boundIndexCount = indexCount;
            _boundStartIndex = startIndex;
            _boundBaseVertex = baseVertex;
            _visibleStateDirty = false;
        }

        private void ClearVisibleState()
        {
            _lastVisibleFloraCount = 0;
            if (_visibleCountReadbackPending && _visibleCountReadbackRequest.done)
            {
                _visibleCountReadbackPending = false;
                _visibleCountReadbackRequest = default;
            }

            if (!_visibleStateDirty)
                return;

            if (_visibleIndexBuffer != null)
                _visibleIndexBuffer.SetCounterValue(0u);
            if (_visibleMatrixBuffer == null)
            {
                _visibleStateDirty = false;
                return;
            }

            _visibleMatrixBuffer.SetCounterValue(0u);
            if (_argsBuffer != null)
                GraphicsBuffer.CopyCount(_visibleMatrixBuffer, _argsBuffer, sizeof(uint));
            _visibleStateDirty = false;
        }

        private int ResolveSafeActiveCount()
        {
            if (!TryResolveMatrixView(out var matrices))
                return 0;

            int safeCount = math.min(_activeInstanceCount, matrices.Length);
            safeCount = TryResolveMetadataView(out var metadata) ? math.min(safeCount, metadata.Length) : safeCount;
            safeCount = TryResolveAgeView(out var ages01) ? math.min(safeCount, ages01.Length) : safeCount;
            safeCount = TryResolvePhaseSeedView(out var phaseSeeds) ? math.min(safeCount, phaseSeeds.Length) : safeCount;
            safeCount = TryResolveVisualPayloadView(out var visualPayload) ? math.min(safeCount, visualPayload.Length) : safeCount;
            return math.clamp(safeCount, 0, instanceCapacity);
        }

        private bool TryUploadVaultBuffers(int activeCount)
        {
            IDataVault vault = _dataVault;
            if (vault == null ||
                !IsMatchingScatterVaultHandle(in _vaultMatricesHandle, BufferID.FloraScatterMatrices) ||
                !IsMatchingScatterVaultHandle(in _vaultMetadataHandle, BufferID.FloraScatterMetadata) ||
                !IsMatchingScatterVaultHandle(in _vaultAgeHandle, BufferID.FloraScatterAge01) ||
                !IsMatchingScatterVaultHandle(in _vaultPhaseSeedHandle, BufferID.FloraScatterPhaseSeeds) ||
                !IsMatchingScatterVaultHandle(in _vaultVisualPayloadHandle, BufferID.FloraScatterVisualPayload))
            {
                return false;
            }

            bool generationChanged = HasVaultGenerationChanged(vault);
            if (!_forceUpload && !generationChanged)
                return true;

            if (!TryResolveMatrixView(out var matrices) ||
                !TryResolveMetadataView(out var metadata) ||
                !TryResolveAgeView(out var ages01) ||
                !TryResolvePhaseSeedView(out var phaseSeeds) ||
                !TryResolveVisualPayloadView(out var visualPayload))
            {
                return false;
            }

            if (!ValidateFiniteMatrices(matrices, activeCount))
                return false;
            if (!ValidateFiniteMetadata(metadata, activeCount) ||
                !ValidateFiniteFloatLane(ages01, activeCount) ||
                !ValidateFiniteFloatLane(phaseSeeds, activeCount) ||
                !ValidateFiniteVector4Lane(visualPayload, activeCount))
            {
                return false;
            }

            int writeIndex = 1 - _gpuBufferIndex;
            GraphicsBufferUploadUtility.UploadNativeArray(_matrixBuffers[writeIndex], matrices, activeCount);
            GraphicsBufferUploadUtility.UploadNativeArray(_metadataBuffers[writeIndex], metadata, activeCount);
            GraphicsBufferUploadUtility.UploadNativeArray(_floraAgeBuffers[writeIndex], ages01, activeCount);
            GraphicsBufferUploadUtility.UploadNativeArray(_floraPhaseSeedBuffers[writeIndex], phaseSeeds, activeCount);
            GraphicsBufferUploadUtility.UploadNativeArray(_floraVisualPayloadBuffers[writeIndex], visualPayload, activeCount);
            _activeFloraAgeBuffer = _floraAgeBuffers[writeIndex];
            _activeFloraPhaseSeedBuffer = _floraPhaseSeedBuffers[writeIndex];
            _activeFloraVisualPayloadBuffer = _floraVisualPayloadBuffers[writeIndex];
            _gpuBufferIndex = writeIndex;
            CaptureVaultGenerations(vault);
            _forceUpload = false;
            return true;
        }

        private bool ValidateFiniteMetadata(NativeArray<GpuScatterFloraInstanceData> metadata, int activeCount)
        {
            for (int i = 0; i < activeCount; i++)
            {
                GpuScatterFloraInstanceData value = metadata[i];
                if (math.all(math.isfinite(new float4(value.Type, value.HeightScale, value.WidthScale, value.Variation))) &&
                    math.all(math.isfinite(new float4(value.TemplateIndex, value.RuntimeState, value.RuntimeFlags, value.PulseFrequency))) &&
                    IsFiniteVector4(value.BioluminescenceColor) &&
                    math.all(math.isfinite(new float4(value.SwaySpeed, value.BendAmplitude, value.HealthNormalized, value.Reserved0))))
                {
                    continue;
                }

                RecordBlackBox(BlackBoxFlagNonFiniteMetadata, activeCount);
                DumpBlackBox(BlackBoxDumpReasonNonFiniteMetadata);
                return false;
            }

            return true;
        }

        private bool ValidateFiniteFloatLane(NativeArray<float> values, int activeCount)
        {
            for (int i = 0; i < activeCount; i++)
            {
                if (math.isfinite(values[i]))
                    continue;

                RecordBlackBox(BlackBoxFlagNonFiniteAuxiliaryLane, activeCount);
                DumpBlackBox(BlackBoxDumpReasonNonFiniteAuxiliaryLane);
                return false;
            }

            return true;
        }

        private bool ValidateFiniteVector4Lane(NativeArray<Vector4> values, int activeCount)
        {
            for (int i = 0; i < activeCount; i++)
            {
                if (IsFiniteVector4(values[i]))
                    continue;

                RecordBlackBox(BlackBoxFlagNonFiniteAuxiliaryLane, activeCount);
                DumpBlackBox(BlackBoxDumpReasonNonFiniteAuxiliaryLane);
                return false;
            }

            return true;
        }

        private bool HasVaultGenerationChanged(IDataVault vault)
        {
            bool matrixGenerationFound = TryReadScatterVaultGeneration<Matrix4x4>(vault, BufferID.FloraScatterMatrices, out uint matrixGeneration);
            bool metadataGenerationFound = TryReadScatterVaultGeneration<GpuScatterFloraInstanceData>(vault, BufferID.FloraScatterMetadata, out uint metadataGeneration);
            bool ageGenerationFound = TryReadScatterVaultGeneration<float>(vault, BufferID.FloraScatterAge01, out uint ageGeneration);
            bool phaseSeedGenerationFound = TryReadScatterVaultGeneration<float>(vault, BufferID.FloraScatterPhaseSeeds, out uint phaseSeedGeneration);
            bool visualPayloadGenerationFound = TryReadScatterVaultGeneration<Vector4>(vault, BufferID.FloraScatterVisualPayload, out uint visualPayloadGeneration);
            bool changed = (!_hasMatrixGeneration && matrixGenerationFound) ||
                           (!_hasMetadataGeneration && metadataGenerationFound) ||
                           (!_hasAgeGeneration && ageGenerationFound) ||
                           (!_hasPhaseSeedGeneration && phaseSeedGenerationFound) ||
                           (!_hasVisualPayloadGeneration && visualPayloadGenerationFound) ||
                           (_hasMatrixGeneration && matrixGenerationFound && matrixGeneration != _lastMatrixGeneration) ||
                           (_hasMetadataGeneration && metadataGenerationFound && metadataGeneration != _lastMetadataGeneration) ||
                           (_hasAgeGeneration && ageGenerationFound && ageGeneration != _lastAgeGeneration) ||
                           (_hasPhaseSeedGeneration && phaseSeedGenerationFound && phaseSeedGeneration != _lastPhaseSeedGeneration) ||
                           (_hasVisualPayloadGeneration && visualPayloadGenerationFound && visualPayloadGeneration != _lastVisualPayloadGeneration);
            return changed;
        }

        private void CaptureVaultGenerations(IDataVault vault)
        {
            _hasMatrixGeneration = TryReadScatterVaultGeneration<Matrix4x4>(vault, BufferID.FloraScatterMatrices, out _lastMatrixGeneration);
            _hasMetadataGeneration = TryReadScatterVaultGeneration<GpuScatterFloraInstanceData>(vault, BufferID.FloraScatterMetadata, out _lastMetadataGeneration);
            _hasAgeGeneration = TryReadScatterVaultGeneration<float>(vault, BufferID.FloraScatterAge01, out _lastAgeGeneration);
            _hasPhaseSeedGeneration = TryReadScatterVaultGeneration<float>(vault, BufferID.FloraScatterPhaseSeeds, out _lastPhaseSeedGeneration);
            _hasVisualPayloadGeneration = TryReadScatterVaultGeneration<Vector4>(vault, BufferID.FloraScatterVisualPayload, out _lastVisualPayloadGeneration);
        }

        private bool ValidateFiniteMatrices(NativeArray<Matrix4x4> matrices, int activeCount)
        {
            for (int i = 0; i < activeCount; i++)
            {
                Matrix4x4 matrix = matrices[i];
                if (IsFiniteMatrix(matrix))
                    continue;

                RecordBlackBox(BlackBoxFlagNonFiniteVaultMatrix, activeCount);
                DumpBlackBox(BlackBoxDumpReasonNonFiniteMatrix);
                return false;
            }

            return true;
        }

        private void DispatchCull(int activeCount)
        {
            int dispatchGroups = ResolveDispatchGroups(activeCount, _dispatchThreadGroupSizeX);
            if (dispatchGroups <= 0)
            {
                RecordBlackBox(BlackBoxFlagInvalidThreadGroup, activeCount);
                return;
            }

            _visibleIndexBuffer.SetCounterValue(0u);
            _visibleMatrixBuffer.SetCounterValue(0u);

            GraphicsBuffer matrixBuffer = _matrixBuffers[_gpuBufferIndex];
            scatterCullCompute.SetBuffer(_scatterCullKernel, _SourceMatricesId, matrixBuffer);
            scatterCullCompute.SetBuffer(_scatterCullKernel, _VisibleIndicesId, _visibleIndexBuffer);
            scatterCullCompute.SetBuffer(_scatterCullKernel, _VisibleMatricesId, _visibleMatrixBuffer);
            scatterCullCompute.SetBuffer(_scatterCullKernel, _MotionVectorsId, _motionVectorBuffer);
            float cullDistance = SanitizePositiveFinite(_effectiveCullDistanceMeters, ResolveDesiredCullDistance());
            UploadScatterFrameConstants(
                activeCount,
                cullDistance * cullDistance,
                ResolveSafeLocalBoundsCenter(),
                ResolveSafeLocalBoundsExtents());

            scatterCullCompute.Dispatch(_scatterCullKernel, dispatchGroups, 1, 1);
            GraphicsBuffer.CopyCount(_visibleMatrixBuffer, _argsBuffer, sizeof(uint));
            _visibleStateDirty = true;
        }

        private void UploadScatterFrameConstants(
            int activeCount,
            float maxDistanceSq,
            Vector3 safeLocalBoundsCenter,
            Vector3 safeLocalBoundsExtents)
        {
            ScatterFrameConstants constants = new ScatterFrameConstants
            {
                Params0 = new Vector4(
                    math.max(0, activeCount),
                    SanitizePositiveFinite(maxDistanceSq, 1f),
                    SanitizeNonNegativeFinite(swayMotionStrength),
                    _frameIndex & 0x00FFFFFF),
                Params1 = new Vector4(_aupShiftOffset.x, _aupShiftOffset.y, _aupShiftOffset.z, _cachedQualityWeight01),
                Params2 = new Vector4(_lastCameraSignalPosition.x, _lastCameraSignalPosition.y, _lastCameraSignalPosition.z, 0f),
                Params3 = new Vector4(safeLocalBoundsCenter.x, safeLocalBoundsCenter.y, safeLocalBoundsCenter.z, 0f),
                Params4 = new Vector4(safeLocalBoundsExtents.x, safeLocalBoundsExtents.y, safeLocalBoundsExtents.z, instanceCapacity),
                FrustumPlane0 = _frustumPlaneUpload[0],
                FrustumPlane1 = _frustumPlaneUpload[1],
                FrustumPlane2 = _frustumPlaneUpload[2],
                FrustumPlane3 = _frustumPlaneUpload[3],
                FrustumPlane4 = _frustumPlaneUpload[4],
                FrustumPlane5 = _frustumPlaneUpload[5]
            };

            if (_coldSupportsSetConstantBuffer &&
                TryResolveFrameConstantsWriteBuffer(out GraphicsBuffer constantsWriteBuffer))
            {
                _frameConstantsUpload[0] = constants;
                GraphicsBufferUploadUtility.UploadArray(constantsWriteBuffer, _frameConstantsUpload, 1);
                _activeFrameConstantsBuffer = constantsWriteBuffer;
                _frameConstantsUploadIndex ^= 1;
                scatterCullCompute.SetConstantBuffer(ScatterFrameConstantsBufferName, _activeFrameConstantsBuffer, 0, ScatterFrameConstantsStrideBytes);
                return;
            }

            scatterCullCompute.SetVector(_ScatterParams0Id, constants.Params0);
            scatterCullCompute.SetVector(_ScatterParams1Id, constants.Params1);
            scatterCullCompute.SetVector(_ScatterParams2Id, constants.Params2);
            scatterCullCompute.SetVector(_ScatterParams3Id, constants.Params3);
            scatterCullCompute.SetVector(_ScatterParams4Id, constants.Params4);
            scatterCullCompute.SetVector(_ScatterFrustumPlane0Id, constants.FrustumPlane0);
            scatterCullCompute.SetVector(_ScatterFrustumPlane1Id, constants.FrustumPlane1);
            scatterCullCompute.SetVector(_ScatterFrustumPlane2Id, constants.FrustumPlane2);
            scatterCullCompute.SetVector(_ScatterFrustumPlane3Id, constants.FrustumPlane3);
            scatterCullCompute.SetVector(_ScatterFrustumPlane4Id, constants.FrustumPlane4);
            scatterCullCompute.SetVector(_ScatterFrustumPlane5Id, constants.FrustumPlane5);
        }

        private bool TryResolveFrameConstantsWriteBuffer(out GraphicsBuffer constantsWriteBuffer)
        {
            constantsWriteBuffer = (_frameConstantsUploadIndex & 1) == 0
                ? _frameConstantsBufferB
                : _frameConstantsBufferA;
            if (constantsWriteBuffer != null && constantsWriteBuffer.IsValid())
                return true;

            constantsWriteBuffer = _frameConstantsBufferA != null && _frameConstantsBufferA.IsValid()
                ? _frameConstantsBufferA
                : _frameConstantsBufferB;
            return constantsWriteBuffer != null && constantsWriteBuffer.IsValid();
        }

        private bool Render(int activeCount)
        {
            Material material = ResolveRenderMaterial();
            Mesh mesh = floraMesh;
            if (material == null || mesh == null || activeCount <= 0)
                return false;

            if (!IsRenderMaterialVariantValid(material))
            {
                ClearVisibleState();
                RecordBlackBox(BlackBoxFlagInvalidMaterialVariant, activeCount);
                return false;
            }

            MaterialPropertyBlock properties = _materialProperties;
            if (properties == null)
                return false;

            properties.Clear();
            properties.SetBuffer(_ShaderInstanceMatricesId, _matrixBuffers[_gpuBufferIndex]);
            properties.SetBuffer(_ShaderInstanceDataId, _metadataBuffers[_gpuBufferIndex]);
            properties.SetBuffer(_ShaderFloraAges01Id, _activeFloraAgeBuffer);
            properties.SetBuffer(_ShaderFloraPhaseSeedsId, _activeFloraPhaseSeedBuffer);
            properties.SetBuffer(_ShaderFloraScatterVisualPayloadId, _activeFloraVisualPayloadBuffer);
            properties.SetFloat(_FloraScatterVisualPayloadEnabledId, _cachedQualityWeight01);
            properties.SetBuffer(_ShaderVisibleIndicesId, _visibleIndexBuffer);
            properties.SetBuffer(_ShaderMotionVectorsId, _motionVectorBuffer);
            properties.SetVector(_GlobalFloatingOffsetId, _aupShiftOffset);
            properties.SetVector(_HectonFloatingOriginOffsetId, _aupShiftOffset);
            properties.SetFloat(_LodNearDistanceId, SanitizePositiveFinite(lowTierCullDistanceMeters, 100f));
            properties.SetFloat(_LodFarDistanceId, SanitizePositiveFinite(_effectiveCullDistanceMeters, ResolveDesiredCullDistance()));
            properties.SetFloat(_LodTransitionRangeId, SanitizeNonNegativeFinite(lodCrossfadeRangeMeters) * _cachedQualityWeight01);
            ApplyOptionalShaderFallbacks(properties);
            ApplyMaterialScalability(properties);

            Bounds bounds = _hasExplicitDrawBounds ? _drawBounds : ResolveFallbackDrawBounds();
            RenderParams renderParams = new RenderParams(material)
            {
                worldBounds = bounds,
                layer = gameObject.layer,
                shadowCastingMode = shadowCastingMode,
                receiveShadows = receiveShadows,
                motionVectorMode = MotionVectorGenerationMode.Object,
                camera = viewCamera,
                matProps = properties
            };

            UnityEngine.Graphics.RenderMeshIndirect(renderParams, mesh, _argsBuffer, 1, 0);
            return true;
        }

        private bool HasAnyConfiguredMaterial()
        {
            return floraMaterial != null ||
                   lowTierFloraMaterial != null ||
                   highTierFloraMaterial != null;
        }

        private Material ResolveRenderMaterial()
        {
            if (floraMaterial != null)
                return floraMaterial;

            return highTierFloraMaterial != null ? highTierFloraMaterial : lowTierFloraMaterial;
        }

        private bool TryValidateRenderMaterialVariant(int activeCount)
        {
            Material material = ResolveRenderMaterial();
            if (material != null &&
                floraMesh != null &&
                activeCount > 0 &&
                IsRenderMaterialVariantValid(material))
            {
                return true;
            }

            ClearVisibleState();
            RecordBlackBox(BlackBoxFlagInvalidMaterialVariant, activeCount);
            return false;
        }

        private bool IsRenderMaterialVariantValid(Material material)
        {
            if (material == null || material.shader == null)
                return false;

            int materialId = material.GetEntityId().GetHashCode();
            bool hasIndirectVariant = material.IsKeywordEnabled(GpuIndirectKeyword);
            bool valid = hasIndirectVariant;

            _materialVariantCacheInitialized = true;
            _cachedMaterialVariantInstanceId = materialId;
            _cachedMaterialVariantValid = valid;
            return valid;
        }

        private static void ApplyOptionalShaderFallbacks(MaterialPropertyBlock properties)
        {
            properties.SetFloat(_FloraSnapFlagsEnabledId, 0f);
            properties.SetInt(_FloraFlowFieldResolutionId, 0);
            properties.SetInt(_FloraInteractionCountId, 0);
            properties.SetInt(_FloraWakeCountId, 0);
            properties.SetInt(_ImpactSphereCountId, 0);
            properties.SetInt(_PredatorAupCountId, 0);
            properties.SetVector(_AbyssalGridResolutionId, Vector4.zero);
            properties.SetFloat(_AbyssalFlowTextureActiveId, 0f);
        }

        private void ApplyMaterialScalability(MaterialPropertyBlock properties)
        {
            float quality = Smooth01(_cachedQualityWeight01);
            properties.SetFloat(_AnisotropicSssStrengthId, math.lerp(SanitizeNonNegativeFinite(lowTierAnisotropicSssStrength), SanitizeNonNegativeFinite(highTierAnisotropicSssStrength), quality));
            properties.SetFloat(_OrganicSssScaleId, math.lerp(SanitizeNonNegativeFinite(lowTierOrganicSssScale), SanitizeNonNegativeFinite(highTierOrganicSssScale), quality));
            properties.SetFloat(_EdgeBloomStrengthId, math.lerp(SanitizeNonNegativeFinite(lowTierEdgeBloomStrength), SanitizeNonNegativeFinite(highTierEdgeBloomStrength), quality));
            properties.SetFloat(_LocalCausticStrengthId, math.lerp(SanitizeNonNegativeFinite(lowTierLocalCausticStrength), SanitizeNonNegativeFinite(highTierLocalCausticStrength), quality));
        }

        private void UpdateVisibleCountReadback(int frameIndex)
        {
            if (_visibleCountReadbackPending)
            {
                if (!_visibleCountReadbackRequest.done)
                    return;

                _visibleCountReadbackPending = false;
                if (!_visibleCountReadbackRequest.hasError && _visibleCountReadback.Data.IsCreated)
                {
                    _lastVisibleFloraCount = _visibleCountReadback.Data.Length > IndirectArgsInstanceCountIndex
                        ? (int)math.min(_visibleCountReadback.Data[IndirectArgsInstanceCountIndex], (uint)int.MaxValue)
                        : 0;
                }

                return;
            }

            if (_argsBuffer == null ||
                (frameIndex % VisibleCountReadbackFrameStride) != 0)
            {
                return;
            }

            if (!HasVisibleCountReadbackData())
            {
                QueueVisibleCountReadbackRepair();
                return;
            }

            _visibleCountReadbackRequest = AsyncGPUReadback.RequestIntoNativeArray(
                ref _visibleCountReadback.Data,
                _argsBuffer,
                IndirectArgsReadbackByteCount,
                0,
                null);
            _visibleCountReadbackPending = !_visibleCountReadbackRequest.hasError;
            if (!_visibleCountReadbackPending)
                _visibleCountReadbackRequest = default;
        }

        private bool EnsureVisibleCountReadbackData()
        {
            if (HasVisibleCountReadbackData())
                return true;

            if (_visibleCountReadbackPending)
                return false;

            DisposeVisibleCountReadbackData();
            _visibleCountReadback.Data = new NativeArray<uint>(
                IndirectArgsElementCount,
                Allocator.Persistent,
                NativeArrayOptions.ClearMemory);
            NativeMemorySentinel.RegisterNativeArray(_visibleCountReadback.Data, nameof(GpuScatterLodManager), "_visibleCountReadbackData", NativeAllocationLifetime.Scene);
            _visibleCountReadbackRepairRequested = false;
            return true;
        }

        private bool HasVisibleCountReadbackData()
        {
            return _visibleCountReadback.Data.IsCreated &&
                   _visibleCountReadback.Data.Length >= IndirectArgsElementCount;
        }

        private void QueueVisibleCountReadbackRepair()
        {
            _visibleCountReadbackRepairRequested = true;
        }

        private void FlushVisibleCountReadbackRepairSlow()
        {
            if (!_visibleCountReadbackRepairRequested && HasVisibleCountReadbackData())
                return;

            if (_argsBuffer == null || _visibleCountReadbackPending)
                return;

            EnsureVisibleCountReadbackData();
        }

        private void CompletePendingVisibleCountReadbackForRelease()
        {
            if (!_visibleCountReadbackPending)
                return;

            if (!_visibleCountReadbackRequest.done)
                _visibleCountReadbackRequest.WaitForCompletion();

            _visibleCountReadbackPending = false;
            _visibleCountReadbackRequest = default;
        }

        private void DisposeVisibleCountReadbackData()
        {
            if (_visibleCountReadback.Data.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_visibleCountReadback.Data);
                _visibleCountReadback.Data.Dispose();
                _visibleCountReadback.Data = default;
            }

            _visibleCountReadbackRepairRequested = false;
        }

        private bool TryBuildFrustumPlanes()
        {
            if (viewCamera != null)
            {
                GeometryUtility.CalculateFrustumPlanes(viewCamera, _cameraPlanes);
                UploadUnityFrustumPlane(0, 0);
                UploadUnityFrustumPlane(1, 1);
                UploadUnityFrustumPlane(2, 2);
                UploadUnityFrustumPlane(3, 3);
                UploadUnityFrustumPlane(4, 4);
                UploadUnityFrustumPlane(5, 5);
                Transform cameraTransform = viewCamera.transform;
                _lastCameraSignalPosition = cameraTransform.position;
                _lastCameraSignalForward = cameraTransform.forward;
                _lastCameraSignalUp = cameraTransform.up;
                _hasCameraSignal = true;
                return ValidateUploadedFrustumPlanes();
            }

            if (!_hasCameraSignal)
                return false;

            BuildFallbackFrustumPlanesFromSignal();
            return ValidateUploadedFrustumPlanes();
        }

        private void UploadUnityFrustumPlane(int targetIndex, int sourceIndex)
        {
            Plane plane = _cameraPlanes[sourceIndex];
            _frustumPlaneUpload[targetIndex] = new Vector4(plane.normal.x, plane.normal.y, plane.normal.z, plane.distance);
        }

        private void BuildFallbackFrustumPlanesFromSignal()
        {
            float3 position = new float3(_lastCameraSignalPosition.x, _lastCameraSignalPosition.y, _lastCameraSignalPosition.z);
            float3 forward = math.normalizesafe(
                new float3(_lastCameraSignalForward.x, _lastCameraSignalForward.y, _lastCameraSignalForward.z),
                new float3(0f, 0f, 1f));
            float3 up = math.normalizesafe(
                new float3(_lastCameraSignalUp.x, _lastCameraSignalUp.y, _lastCameraSignalUp.z),
                new float3(0f, 1f, 0f));
            float3 right = math.normalizesafe(math.cross(up, forward), new float3(1f, 0f, 0f));
            up = math.normalizesafe(math.cross(forward, right), new float3(0f, 1f, 0f));

            float nearClip = SanitizePositiveFinite(_lastCameraSignalNearMeters, 0.03f);
            nearClip = math.max(0.01f, nearClip);
            float effectiveCullDistance = SanitizePositiveFinite(_effectiveCullDistanceMeters, ResolveDesiredCullDistance());
            float signalFarClip = SanitizePositiveFinite(_lastCameraSignalFarMeters, effectiveCullDistance);
            float farClip = math.max(nearClip + 1f, math.min(signalFarClip, effectiveCullDistance));
            float signalFovDegrees = math.isfinite(_lastCameraSignalFovDegrees) ? _lastCameraSignalFovDegrees : 70f;
            float verticalTan = global::Hecton8.Core.MathLodApproximation.ApproxTanClamped(math.radians(math.clamp(signalFovDegrees, 5f, 160f) * 0.5f), 4096f);
            float nearHalfY = verticalTan * nearClip;
            float nearHalfX = nearHalfY * SanitizePositiveFinite(fallbackAspect, DefaultFallbackAspect);

            float3 nearCenter = position + forward * nearClip;
            float3 farCenter = position + forward * farClip;
            float3 leftRay = math.normalizesafe(forward * nearClip - right * nearHalfX, forward);
            float3 rightRay = math.normalizesafe(forward * nearClip + right * nearHalfX, forward);
            float3 topRay = math.normalizesafe(forward * nearClip + up * nearHalfY, forward);
            float3 bottomRay = math.normalizesafe(forward * nearClip - up * nearHalfY, forward);

            WritePlane(0, forward, nearCenter);
            WritePlane(1, -forward, farCenter);
            WritePlane(2, math.normalizesafe(math.cross(up, leftRay), right), position);
            WritePlane(3, math.normalizesafe(math.cross(rightRay, up), -right), position);
            WritePlane(4, math.normalizesafe(math.cross(right, topRay), -up), position);
            WritePlane(5, math.normalizesafe(math.cross(bottomRay, right), up), position);
        }

        private void WritePlane(int index, float3 normal, float3 point)
        {
            float distance = -math.dot(normal, point);
            _frustumPlaneUpload[index] = new Vector4(normal.x, normal.y, normal.z, distance);
        }

        private bool ValidateUploadedFrustumPlanes()
        {
            for (int i = 0; i < FrustumPlaneCount; i++)
            {
                if (IsFiniteVector4(_frustumPlaneUpload[i]))
                    continue;

                return false;
            }

            return true;
        }

        private void ConsumeCameraFrustumSignals()
        {
            ReadOnlySpan<CameraFrustumSignal> signals = SignalBus<CameraFrustumSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                CameraFrustumSignal signal = signals[i];
                if (!math.all(math.isfinite(signal.Position)) ||
                    !math.all(math.isfinite(signal.Forward)) ||
                    !math.all(math.isfinite(signal.Up)))
                {
                    continue;
                }

                _lastCameraSignalPosition = new Vector3(signal.Position.x, signal.Position.y, signal.Position.z);
                _lastCameraSignalForward = new Vector3(signal.Forward.x, signal.Forward.y, signal.Forward.z);
                _lastCameraSignalUp = new Vector3(signal.Up.x, signal.Up.y, signal.Up.z);
                _lastCameraSignalFovDegrees = math.isfinite(signal.FieldOfViewDegrees) ? signal.FieldOfViewDegrees : 70f;
                _lastCameraSignalNearMeters = math.isfinite(signal.NearClipMeters) ? signal.NearClipMeters : 0.03f;
                _lastCameraSignalFarMeters = math.isfinite(signal.FarClipMeters) ? signal.FarClipMeters : _effectiveCullDistanceMeters;
                _hasCameraSignal = true;
            }
        }

        private void ConsumeSystemHealthSignals()
        {
            float stress01 = _externalSystemStress01;
            ReadOnlySpan<SystemHealthSignal> healthSignals = SignalBus<SystemHealthSignal>.GetFrameSnapshot();
            for (int i = 0; i < healthSignals.Length; i++)
            {
                SystemHealthSignal signal = healthSignals[i];
                float health01 = Sanitize01(signal.SystemHealthIndex01);
                stress01 = math.max(stress01, 1f - health01);
                stress01 = math.max(stress01, math.saturate(signal.PressureLevel * 0.25f));
            }

            _systemStress01 = Sanitize01(stress01);
        }

        private void RefreshContinuousQualityPolicy(bool forceCommit)
        {
            float quality = ResolveGlobalQualityWeight01();
            _pendingQualityWeight01 = quality;

            if (_qualityCacheInitialized && !forceCommit)
                return;

            _cachedQualityWeight01 = quality;
            _qualityCacheInitialized = true;
            _cullDistanceHysteresisTimer = 0f;
        }

        private static float ResolveGlobalQualityWeight01()
        {
            float signalWeight = SignalBusRegistry.GlobalQualityWeight01;
            if (math.isfinite(signalWeight) && signalWeight > 0f)
                return math.saturate(signalWeight);

            float brainWeight = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.isfinite(brainWeight) ? brainWeight : 1f);
        }

        private void UpdateCullDistance(float deltaTime)
        {
            if (math.abs(_pendingQualityWeight01 - _cachedQualityWeight01) > 0.02f)
            {
                _cullDistanceHysteresisTimer += SanitizeNonNegativeFinite(deltaTime);
                if (_cullDistanceHysteresisTimer >= CullingHysteresisSeconds)
                {
                    _cachedQualityWeight01 = _pendingQualityWeight01;
                    _cullDistanceHysteresisTimer = 0f;
                }
            }

            float desired = ResolveDesiredCullDistance();
            if (_systemStress01 > 0.8f)
                desired *= 0.5f;

            desired = SanitizePositiveFinite(desired, 100f);
            if (_effectiveCullDistanceMeters <= 0f)
            {
                _effectiveCullDistanceMeters = desired;
                _pendingCullDistanceMeters = desired;
                return;
            }

            if (math.abs(desired - _effectiveCullDistanceMeters) <= CullingHysteresisMeters)
            {
                _pendingCullDistanceMeters = desired;
                return;
            }

            if (math.abs(desired - _pendingCullDistanceMeters) > 0.01f)
            {
                _pendingCullDistanceMeters = desired;
                _cullDistanceHysteresisTimer = 0f;
                return;
            }

            _cullDistanceHysteresisTimer += SanitizeNonNegativeFinite(deltaTime);
            if (_cullDistanceHysteresisTimer >= CullingHysteresisSeconds)
            {
                _effectiveCullDistanceMeters = desired;
                _cullDistanceHysteresisTimer = 0f;
            }
        }

        private float ResolveDesiredCullDistance()
        {
            float quality = Smooth01(_cachedQualityWeight01);
            float lowCull = SanitizePositiveFinite(lowTierCullDistanceMeters, 100f);
            float midCull = SanitizePositiveFinite(midTierCullDistanceMeters, 250f);
            float highCull = SanitizePositiveFinite(highTierCullDistanceMeters, 500f);
            float lowToMid = math.lerp(lowCull, midCull, math.smoothstep(0.15f, 0.65f, quality));
            return math.lerp(lowToMid, highCull, math.smoothstep(0.55f, 1f, quality));
        }

        private Bounds ResolveFallbackDrawBounds()
        {
            float effectiveCullDistance = SanitizePositiveFinite(_effectiveCullDistanceMeters, ResolveDesiredCullDistance());
            float diameter = math.max(2f, effectiveCullDistance * 2f);
            Vector3 safeLocalBoundsExtents = ResolveSafeLocalBoundsExtents();
            float height = math.max(8f, safeLocalBoundsExtents.y * 4f);
            if (IsFiniteBounds(fallbackDrawBounds))
            {
                Vector3 fallbackSize = fallbackDrawBounds.size;
                diameter = math.max(diameter, math.max(fallbackSize.x, fallbackSize.z));
                height = math.max(height, fallbackSize.y);
            }

            return new Bounds(_lastCameraSignalPosition, new Vector3(diameter, height, diameter));
        }

        private Vector3 ResolveSafeLocalBoundsCenter()
        {
            return IsFiniteVector(localBoundsCenter) ? localBoundsCenter : Vector3.zero;
        }

        private Vector3 ResolveSafeLocalBoundsExtents()
        {
            return new Vector3(
                ResolveSafePositiveExtent(localBoundsExtents.x),
                ResolveSafePositiveExtent(localBoundsExtents.y),
                ResolveSafePositiveExtent(localBoundsExtents.z));
        }

        private static float ResolveSafePositiveExtent(float value)
        {
            return math.isfinite(value) ? math.max(0.01f, math.abs(value)) : 0.01f;
        }

        private bool TryResolveMatrixView(out NativeArray<Matrix4x4> matrices)
        {
            IDataVault vault = _dataVault;
            return TryResolveScatterVaultBuffer(vault, ref _vaultMatricesHandle, BufferID.FloraScatterMatrices, 1, out matrices);
        }

        private bool TryResolveMetadataView(out NativeArray<GpuScatterFloraInstanceData> metadata)
        {
            IDataVault vault = _dataVault;
            return TryResolveScatterVaultBuffer(vault, ref _vaultMetadataHandle, BufferID.FloraScatterMetadata, 1, out metadata);
        }

        private bool TryResolveAgeView(out NativeArray<float> ages01)
        {
            IDataVault vault = _dataVault;
            return TryResolveScatterVaultBuffer(vault, ref _vaultAgeHandle, BufferID.FloraScatterAge01, 1, out ages01);
        }

        private bool TryResolvePhaseSeedView(out NativeArray<float> phaseSeeds)
        {
            IDataVault vault = _dataVault;
            return TryResolveScatterVaultBuffer(vault, ref _vaultPhaseSeedHandle, BufferID.FloraScatterPhaseSeeds, 1, out phaseSeeds);
        }

        private bool TryResolveVisualPayloadView(out NativeArray<Vector4> visualPayload)
        {
            IDataVault vault = _dataVault;
            return TryResolveScatterVaultBuffer(vault, ref _vaultVisualPayloadHandle, BufferID.FloraScatterVisualPayload, 1, out visualPayload);
        }

        private bool TryResolveCpuFrustumPlaneView(out NativeArray<float4> frustumPlanes)
        {
            IDataVault vault = _dataVault;
            return TryResolveScatterVaultBuffer(vault, ref _cpuFrustumPlanesHandle, BufferID.FloraScatterCpuFrustumPlanes, FrustumPlaneCount, out frustumPlanes);
        }

        private bool TryResolveCpuVisibilityMaskView(out NativeArray<byte> visibilityMask)
        {
            IDataVault vault = _dataVault;
            return TryResolveScatterVaultBuffer(vault, ref _cpuVisibilityMaskHandle, BufferID.FloraScatterCpuVisibilityMask, 1, out visibilityMask);
        }

        private void EnsureMetadataDefaults()
        {
            if (!TryResolveMetadataView(out var metadata) ||
                _metadataDefaultsInitialized ||
                metadata.Length <= 0)
            {
                return;
            }

            GpuScatterFloraInstanceData first = metadata[0];
            if (math.isfinite(first.HeightScale) && first.HeightScale > 0f && first.WidthScale > 0f)
            {
                _metadataDefaultsInitialized = true;
                return;
            }

            int count = math.min(instanceCapacity, metadata.Length);
            for (int i = 0; i < count; i++)
                metadata[i] = GpuScatterFloraInstanceData.CreateDefault(i);

            _metadataDefaultsInitialized = true;
        }

        private void EnsureAuxiliaryShaderLaneDefaults(bool needsDefaultAges, bool needsDefaultPhaseSeeds)
        {
            bool hasAgeView = TryResolveAgeView(out var ages01);
            bool hasPhaseSeedView = TryResolvePhaseSeedView(out var phaseSeeds);
            if (_auxiliaryShaderLanesInitialized ||
                (!needsDefaultAges && !needsDefaultPhaseSeeds) ||
                (needsDefaultAges && !hasAgeView) ||
                (needsDefaultPhaseSeeds && !hasPhaseSeedView))
            {
                _auxiliaryShaderLanesInitialized = !needsDefaultAges && !needsDefaultPhaseSeeds;
                return;
            }

            if (needsDefaultAges)
            {
                int ageCount = math.min(instanceCapacity, ages01.Length);
                for (int i = 0; i < ageCount; i++)
                    ages01[i] = 1f;
            }

            if (needsDefaultPhaseSeeds)
            {
                int seedCount = math.min(instanceCapacity, phaseSeeds.Length);
                for (int i = 0; i < seedCount; i++)
                    phaseSeeds[i] = Hash01((uint)i * 2246822519u + 3266489917u);
            }

            _auxiliaryShaderLanesInitialized = true;
        }

        private void EnsureVisualPayloadDefaults(bool needsDefaultVisualPayload)
        {
            if (_visualPayloadDefaultsInitialized ||
                !needsDefaultVisualPayload)
            {
                _visualPayloadDefaultsInitialized = true;
                return;
            }

            if (!TryResolveVisualPayloadView(out var visualPayload) ||
                visualPayload.Length <= 0)
            {
                return;
            }

            int count = math.min(instanceCapacity, visualPayload.Length);
            for (int i = 0; i < count; i++)
            {
                uint index = (uint)i;
                visualPayload[i] = new Vector4(
                    Hash01(index * 3266489917u + 668265263u),
                    Hash01(index * 2246822519u + 374761393u),
                    Hash01(index * 747796405u + 2891336453u),
                    Hash01(index * 1103515245u + 12345u));
            }

            _visualPayloadDefaultsInitialized = true;
        }

        private bool EnsureBlackBox(IDataVault vault)
        {
            if (vault == null)
                return false;

            if (TryResolveScatterVaultBuffer(vault, ref _blackBoxHandle, BufferID.FloraScatterBlackBox, TelemetryCapacity, out NativeArray<ScatterBlackBoxEntry> _))
                return true;

            if (!TryAcquireScatterVaultBuffer(vault, ref _blackBoxHandle, BufferID.FloraScatterBlackBox, TelemetryCapacity, NativeArrayOptions.ClearMemory, out NativeArray<ScatterBlackBoxEntry> _))
                return false;

            _blackBoxCursor = 0;
            return true;
        }

        private void RecordBlackBox(uint flags, int activeCount)
        {
            if (!TryEnsureBlackBoxView(out NativeArray<ScatterBlackBoxEntry> blackBox))
                return;

            flags |= _gpuReady ? BlackBoxFlagGpuReady : 0u;
            flags |= _hasCameraSignal ? BlackBoxFlagCameraSignal : 0u;
            flags |= _systemStress01 > 0.8f ? BlackBoxFlagStressShed : 0u;

            int blackBoxLength = blackBox.Length;
            int index = _blackBoxCursor % blackBoxLength;
            blackBox[index] = new ScatterBlackBoxEntry
            {
                Frame = unchecked((int)Hecton8.Core.SystemDispatcher.CurrentFrameId),
                ActiveInstanceCount = activeCount,
                VisibleFloraCount = _lastVisibleFloraCount,
                CullDistanceMeters = _effectiveCullDistanceMeters,
                SystemStress01 = _systemStress01,
                CameraPosition = new float3(_lastCameraSignalPosition.x, _lastCameraSignalPosition.y, _lastCameraSignalPosition.z),
                AupShiftOffset = new float3(_aupShiftOffset.x, _aupShiftOffset.y, _aupShiftOffset.z),
                MatrixGeneration = _lastMatrixGeneration,
                MetadataGeneration = _lastMetadataGeneration,
                Flags = flags,
                AuxiliaryGenerationHash = CombineGenerationHash(_lastAgeGeneration, _lastPhaseSeedGeneration),
                VisualPayloadGeneration = _lastVisualPayloadGeneration
            };
            _blackBoxCursor = (_blackBoxCursor + 1) % blackBoxLength;
        }

        private uint BuildRuntimeFlags()
        {
            uint flags = 0u;
            flags |= _gpuReady ? BlackBoxFlagGpuReady : 0u;
            flags |= _hasCameraSignal ? BlackBoxFlagCameraSignal : 0u;
            flags |= _systemStress01 > 0.8f ? BlackBoxFlagStressShed : 0u;
            flags |= _materialVariantCacheInitialized && !_cachedMaterialVariantValid ? BlackBoxFlagInvalidMaterialVariant : 0u;
            return flags;
        }

        private void DumpBlackBox(uint reason)
        {
            if (_blackBoxDumped || !TryEnsureBlackBoxView(out NativeArray<ScatterBlackBoxEntry> blackBox))
                return;

            _blackBoxDumped = true;
            try
            {
                string path = ResolveAgentLogPath("Dump_GPU_SCATTER_LOD_MANAGER.bin");
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
                using BinaryWriter writer = new BinaryWriter(stream);
                writer.Write(BlackBoxMagic);
                writer.Write(BlackBoxVersion);
                writer.Write(reason);
                int blackBoxLength = blackBox.Length;
                writer.Write(blackBoxLength);
                writer.Write(_blackBoxCursor);
                for (int i = 0; i < blackBoxLength; i++)
                {
                    int ringIndex = _blackBoxCursor + i;
                    if (ringIndex >= blackBoxLength)
                        ringIndex -= blackBoxLength;

                    ScatterBlackBoxEntry entry = blackBox[ringIndex];
                    writer.Write(entry.Frame);
                    writer.Write(entry.ActiveInstanceCount);
                    writer.Write(entry.VisibleFloraCount);
                    writer.Write(entry.CullDistanceMeters);
                    writer.Write(entry.SystemStress01);
                    writer.Write(entry.CameraPosition.x);
                    writer.Write(entry.CameraPosition.y);
                    writer.Write(entry.CameraPosition.z);
                    writer.Write(entry.AupShiftOffset.x);
                    writer.Write(entry.AupShiftOffset.y);
                    writer.Write(entry.AupShiftOffset.z);
                    writer.Write(entry.MatrixGeneration);
                    writer.Write(entry.MetadataGeneration);
                    writer.Write(entry.Flags);
                    writer.Write(entry.AuxiliaryGenerationHash);
                    writer.Write(entry.VisualPayloadGeneration);
                }
            }
            catch (Exception)
            {
                GlobalTelemetryBus.PublishMathGuardInvalidNumber(unchecked((int)reason));
            }
        }

        private bool TryEnsureBlackBoxView(out NativeArray<ScatterBlackBoxEntry> blackBox)
        {
            blackBox = default;
            IDataVault vault = _dataVault;
            if (!EnsureBlackBox(vault))
                return false;

            return TryResolveScatterVaultBuffer(vault, ref _blackBoxHandle, BufferID.FloraScatterBlackBox, TelemetryCapacity, out blackBox);
        }

        private bool EnsureCpuAuditBuffers(int activeCount)
        {
            if (!enableBurstCullAudit)
                return false;

            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            if (!TryAcquireScatterVaultBuffer(vault, ref _cpuFrustumPlanesHandle, BufferID.FloraScatterCpuFrustumPlanes, FrustumPlaneCount, NativeArrayOptions.UninitializedMemory, out NativeArray<float4> _))
                return false;

            int visibilityCapacity = math.max(activeCount, instanceCapacity);
            return TryAcquireScatterVaultBuffer(vault, ref _cpuVisibilityMaskHandle, BufferID.FloraScatterCpuVisibilityMask, visibilityCapacity, NativeArrayOptions.UninitializedMemory, out NativeArray<byte> _);
        }

        private void UploadCpuFrustumPlanes()
        {
            if (!TryResolveCpuFrustumPlaneView(out var frustumPlanes))
                return;

            for (int i = 0; i < FrustumPlaneCount; i++)
            {
                Vector4 plane = _frustumPlaneUpload[i];
                frustumPlanes[i] = new float4(plane.x, plane.y, plane.z, plane.w);
            }
        }

        private void ReleaseCpuAuditBuffers()
        {
            _cpuFrustumPlanesHandle = default;
            _cpuVisibilityMaskHandle = default;
        }

        private int ResolveKernel(ComputeShader compute, string kernelName)
        {
            if (compute == null || !_coldSupportsComputeShaders || !compute.HasKernel(kernelName))
                return -1;

            int kernel = compute.FindKernel(kernelName);
            return kernel >= 0 && compute.IsSupported(kernel) ? kernel : -1;
        }

        private bool TryResolveDispatchThreadGroupSize()
        {
            if (scatterCullCompute == null ||
                _scatterCullKernel < 0 ||
                !_coldSupportsComputeShaders ||
                !scatterCullCompute.IsSupported(_scatterCullKernel))
                return false;

            scatterCullCompute.GetKernelThreadGroupSizes(_scatterCullKernel, out uint groupX, out uint groupY, out uint groupZ);
            ulong totalThreads = (ulong)groupX * groupY * groupZ;
            if (groupX == 0u ||
                groupY != 1u ||
                groupZ != 1u ||
                totalThreads > PortableMaxThreadsPerThreadGroup ||
                groupX > int.MaxValue)
            {
                _dispatchThreadGroupSizeX = 0;
                RecordBlackBox(BlackBoxFlagInvalidThreadGroup, ResolveSafeActiveCount());
                return false;
            }

            _dispatchThreadGroupSizeX = (int)groupX;
            return true;
        }

        private static int ResolveDispatchGroups(int count, int threadGroupSize)
        {
            if (count <= 0 || threadGroupSize <= 0)
                return 0;

            long groups = ((long)count + threadGroupSize - 1L) / threadGroupSize;
            return groups <= MaxDispatchGroupsPerDimension ? (int)groups : 0;
        }

        private static bool IsFiniteBounds(Bounds bounds)
        {
            Vector3 center = bounds.center;
            Vector3 size = bounds.size;
            return IsFiniteVector(center) && IsFiniteVector(size) && size.x > 0f && size.y > 0f && size.z > 0f;
        }

        private static bool IsFiniteVector(Vector3 value)
        {
            return math.all(math.isfinite(new float3(value.x, value.y, value.z)));
        }

        private static bool IsFiniteVector4(Vector4 value)
        {
            return math.all(math.isfinite(new float4(value.x, value.y, value.z, value.w)));
        }

        private static bool IsFiniteMatrix(Matrix4x4 matrix)
        {
            return math.all(math.isfinite(new float4(matrix.m00, matrix.m01, matrix.m02, matrix.m03))) &&
                   math.all(math.isfinite(new float4(matrix.m10, matrix.m11, matrix.m12, matrix.m13))) &&
                   math.all(math.isfinite(new float4(matrix.m20, matrix.m21, matrix.m22, matrix.m23))) &&
                   math.all(math.isfinite(new float4(matrix.m30, matrix.m31, matrix.m32, matrix.m33)));
        }

        private static float Sanitize01(float value)
        {
            return math.isfinite(value) ? math.saturate(value) : 0f;
        }

        private static float Smooth01(float value)
        {
            float x = math.saturate(math.isfinite(value) ? value : 0f);
            return x * x * (3f - 2f * x);
        }

        private static float SanitizeNonNegativeFinite(float value)
        {
            return math.isfinite(value) ? math.max(0f, value) : 0f;
        }

        private static float SanitizePositiveFinite(float value, float fallback)
        {
            float safeFallback = math.isfinite(fallback) && fallback > 0f ? fallback : 1f;
            return math.isfinite(value) && value > 0f ? value : safeFallback;
        }

        private static float Hash01(uint value)
        {
            value ^= value >> 16;
            value *= 0x7feb352du;
            value ^= value >> 15;
            value *= 0x846ca68bu;
            value ^= value >> 16;
            return (value & 0xFFFFu) * (1f / 65535f);
        }

        private static string ResolveAgentLogPath(string fileName)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Directory.GetCurrentDirectory();
            return Path.Combine(projectRoot, "Docs", "AgentLogs", fileName);
        }

        private static uint CombineGenerationHash(uint generationA, uint generationB)
        {
            uint value = generationA ^ 0x9E3779B9u;
            value ^= generationB + 0x85EBCA6Bu + (value << 6) + (value >> 2);
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return value;
        }

        private void RefreshAupOffsetCold()
        {
            if (TryToVector3(HectonFloatingOrigin.CurrentTotalOffsetDouble, out Vector3 aupShiftOffset))
            {
                _aupShiftOffset = aupShiftOffset;
                return;
            }

            _aupShiftOffset = Vector3.zero;
            RecordBlackBox(BlackBoxFlagNonFiniteAupShift, ResolveSafeActiveCount());
            DumpBlackBox(BlackBoxDumpReasonNonFiniteAup);
        }

        private bool ValidateAbiLayoutCold()
        {
            bool valid =
                UnsafeUtility.SizeOf<Matrix4x4>() == Matrix4x4StrideBytes &&
                UnsafeUtility.SizeOf<Vector4>() == UnsafeSizeOfVector4() &&
                UnsafeUtility.SizeOf<GpuScatterFloraInstanceData>() == GpuScatterFloraInstanceData.Stride &&
                UnsafeUtility.SizeOf<ScatterFrameConstants>() == ScatterFrameConstantsStrideBytes &&
                UnsafeUtility.SizeOf<ScatterBlackBoxEntry>() == ScatterBlackBoxEntryStrideBytes;
            if (valid)
                return true;

            GlobalTelemetryBus.PublishMathGuardInvalidNumber(unchecked((int)BlackBoxDumpReasonAbiLayout));
            return false;
        }

        private static bool TryToVector3(double3 value, out Vector3 result)
        {
            if (!IsFiniteRenderableDouble(value.x) ||
                !IsFiniteRenderableDouble(value.y) ||
                !IsFiniteRenderableDouble(value.z))
            {
                result = Vector3.zero;
                return false;
            }

            result = new Vector3((float)value.x, (float)value.y, (float)value.z);
            return true;
        }

        private static bool IsFiniteRenderableDouble(double value)
        {
            return !double.IsNaN(value) &&
                   !double.IsInfinity(value) &&
                   value <= float.MaxValue &&
                   value >= -float.MaxValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int UnsafeSizeOfVector4()
        {
            return 16;
        }

        private static int Matrix4x4StrideBytes => 64;

        [StructLayout(LayoutKind.Explicit, Size = ScatterFrameConstantsStrideBytes)]
        private struct ScatterFrameConstants
        {
            [FieldOffset(0)]
            public Vector4 Params0;
            [FieldOffset(16)]
            public Vector4 Params1;
            [FieldOffset(32)]
            public Vector4 Params2;
            [FieldOffset(48)]
            public Vector4 Params3;
            [FieldOffset(64)]
            public Vector4 Params4;
            [FieldOffset(80)]
            public Vector4 FrustumPlane0;
            [FieldOffset(96)]
            public Vector4 FrustumPlane1;
            [FieldOffset(112)]
            public Vector4 FrustumPlane2;
            [FieldOffset(128)]
            public Vector4 FrustumPlane3;
            [FieldOffset(144)]
            public Vector4 FrustumPlane4;
            [FieldOffset(160)]
            public Vector4 FrustumPlane5;
        }

        [StructLayout(LayoutKind.Explicit, Size = ScatterBlackBoxEntryStrideBytes)]
        private struct ScatterBlackBoxEntry
        {
            [FieldOffset(0)]
            public int Frame;
            [FieldOffset(4)]
            public int ActiveInstanceCount;
            [FieldOffset(8)]
            public int VisibleFloraCount;
            [FieldOffset(12)]
            public float CullDistanceMeters;
            [FieldOffset(16)]
            public float SystemStress01;
            [FieldOffset(20)]
            public float3 CameraPosition;
            [FieldOffset(32)]
            public float3 AupShiftOffset;
            [FieldOffset(44)]
            public uint MatrixGeneration;
            [FieldOffset(48)]
            public uint MetadataGeneration;
            [FieldOffset(52)]
            public uint Flags;
            [FieldOffset(56)]
            public uint AuxiliaryGenerationHash;
            [FieldOffset(60)]
            public uint VisualPayloadGeneration;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct ScatterCullJob : IJobParallelFor
        {
            [ReadOnly, NoAlias] public NativeArray<Matrix4x4> Matrices;
            [ReadOnly, NoAlias] public NativeArray<float4> CullingPlanes;
            [WriteOnly, NoAlias] public NativeArray<byte> VisibilityMask;
            public int InstanceCount;
            public float3 AupShiftOffset;
            public float3 CameraPosition;
            public float3 LocalBoundsCenter;
            public float3 LocalBoundsExtents;
            public float MaxDistanceSq;

            public void Execute(int index)
            {
                if (index >= InstanceCount)
                    return;

                Matrix4x4 matrix = Matrices[index];
                if (!HasFiniteMatrix(matrix) ||
                    !HasUsableScale(matrix) ||
                    !math.all(math.isfinite(LocalBoundsCenter)) ||
                    !math.all(math.isfinite(LocalBoundsExtents)))
                {
                    VisibilityMask[index] = 0;
                    return;
                }

                float3 center = TransformPoint(matrix, LocalBoundsCenter) + AupShiftOffset;
                if (!math.all(math.isfinite(center)))
                {
                    VisibilityMask[index] = 0;
                    return;
                }

                float distanceSq = math.lengthsq(center - CameraPosition);
                if (!math.isfinite(MaxDistanceSq) ||
                    MaxDistanceSq <= 0f ||
                    !math.isfinite(distanceSq) ||
                    distanceSq > MaxDistanceSq ||
                    !BoundsVisible(matrix, center))
                {
                    VisibilityMask[index] = 0;
                    return;
                }

                VisibilityMask[index] = 1;
            }

            private bool BoundsVisible(Matrix4x4 matrix, float3 center)
            {
                float3 axisX = new float3(matrix.m00, matrix.m10, matrix.m20) * LocalBoundsExtents.x;
                float3 axisY = new float3(matrix.m01, matrix.m11, matrix.m21) * LocalBoundsExtents.y;
                float3 axisZ = new float3(matrix.m02, matrix.m12, matrix.m22) * LocalBoundsExtents.z;
                for (int planeIndex = 0; planeIndex < FrustumPlaneCount; planeIndex++)
                {
                    float4 plane = CullingPlanes[planeIndex];
                    if (!math.all(math.isfinite(plane)))
                        return false;

                    float signedDistance = math.dot(plane.xyz, center) + plane.w;
                    float radius = math.abs(math.dot(plane.xyz, axisX)) +
                                   math.abs(math.dot(plane.xyz, axisY)) +
                                   math.abs(math.dot(plane.xyz, axisZ));
                    if (signedDistance + radius < 0f)
                        return false;
                }

                return true;
            }

            private static float3 TransformPoint(Matrix4x4 matrix, float3 point)
            {
                return new float3(
                    matrix.m00 * point.x + matrix.m01 * point.y + matrix.m02 * point.z + matrix.m03,
                    matrix.m10 * point.x + matrix.m11 * point.y + matrix.m12 * point.z + matrix.m13,
                    matrix.m20 * point.x + matrix.m21 * point.y + matrix.m22 * point.z + matrix.m23);
            }

            private static bool HasUsableScale(Matrix4x4 matrix)
            {
                float3 axisX = new float3(matrix.m00, matrix.m10, matrix.m20);
                float3 axisY = new float3(matrix.m01, matrix.m11, matrix.m21);
                float3 axisZ = new float3(matrix.m02, matrix.m12, matrix.m22);
                float scaleXSq = math.lengthsq(axisX);
                float scaleYSq = math.lengthsq(axisY);
                float scaleZSq = math.lengthsq(axisZ);
                return math.isfinite(scaleXSq) &&
                       math.isfinite(scaleYSq) &&
                       math.isfinite(scaleZSq) &&
                       scaleXSq > 0.000001f &&
                       scaleYSq > 0.000001f &&
                       scaleZSq > 0.000001f;
            }

            private static bool HasFiniteMatrix(Matrix4x4 matrix)
            {
                return math.all(math.isfinite(new float4(matrix.m00, matrix.m01, matrix.m02, matrix.m03))) &&
                       math.all(math.isfinite(new float4(matrix.m10, matrix.m11, matrix.m12, matrix.m13))) &&
                       math.all(math.isfinite(new float4(matrix.m20, matrix.m21, matrix.m22, matrix.m23))) &&
                       math.all(math.isfinite(new float4(matrix.m30, matrix.m31, matrix.m32, matrix.m33)));
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            instanceCapacity = math.max(1, instanceCapacity);
            initialActiveInstanceCount = math.clamp(initialActiveInstanceCount, 0, instanceCapacity);
            lowTierCullDistanceMeters = SanitizePositiveFinite(lowTierCullDistanceMeters, 100f);
            midTierCullDistanceMeters = math.max(lowTierCullDistanceMeters, SanitizePositiveFinite(midTierCullDistanceMeters, 250f));
            highTierCullDistanceMeters = math.max(midTierCullDistanceMeters, SanitizePositiveFinite(highTierCullDistanceMeters, 500f));
            fallbackAspect = SanitizePositiveFinite(fallbackAspect, DefaultFallbackAspect);
            lodCrossfadeRangeMeters = SanitizeNonNegativeFinite(lodCrossfadeRangeMeters);
            swayMotionStrength = SanitizeNonNegativeFinite(swayMotionStrength);
            localBoundsExtents = new Vector3(
                ResolveSafePositiveExtent(localBoundsExtents.x),
                ResolveSafePositiveExtent(localBoundsExtents.y),
                ResolveSafePositiveExtent(localBoundsExtents.z));
            if (!IsFiniteVector(localBoundsCenter))
                localBoundsCenter = Vector3.zero;
            _materialVariantCacheInitialized = false;
        }
#endif
    }
}
