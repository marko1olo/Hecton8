using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Core.Contracts.Signals;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.Rendering.Scatter
{
    /// <summary>
    /// Public metadata payload consumed by the indirect flora shader and the scatter DataVault seam.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = Stride)]
    public struct GpuScatterFloraInstanceData
    {
        /// <summary>GPU stride in bytes.</summary>
        public const int Stride = 64;

        /// <summary>Vegetation type flag: 0 grass, 1 kelp, 2 sargassum.</summary>
        public float Type;

        /// <summary>Height scalar consumed by the flora material.</summary>
        public float HeightScale;

        /// <summary>Width scalar consumed by the flora material.</summary>
        public float WidthScale;

        /// <summary>Stable randomization seed in 0..1.</summary>
        public float Variation;

        /// <summary>Optional template index. Negative means producer did not bind a template.</summary>
        public float TemplateIndex;

        /// <summary>Shader runtime state lane.</summary>
        public float RuntimeState;

        /// <summary>Packed runtime flags lane.</summary>
        public float RuntimeFlags;

        /// <summary>Bioluminescence pulse frequency in Hertz.</summary>
        public float PulseFrequency;

        /// <summary>Bioluminescence color and intensity payload.</summary>
        public Vector4 BioluminescenceColor;

        /// <summary>Sway speed multiplier.</summary>
        public float SwaySpeed;

        /// <summary>Bend amplitude multiplier.</summary>
        public float BendAmplitude;

        /// <summary>Health lane in 0..1.</summary>
        public float HealthNormalized;

        /// <summary>Reserved producer lane.</summary>
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
        IUpdatable,
        IOriginShiftListener,
        IGlobalRegistryHotSwapListener,
        IGlobalRegistryHotSwapRefListener,
        IScalabilityChangedEventListener
    {
        private const int DefaultInstanceCapacity = 100000;
        private const int DoubleBufferCount = 2;
        private const int FrustumPlaneCount = 6;
        private const int TelemetryCapacity = 300;
        private const int ThreadGroupSize = 64;
        private const int VisibleCountReadbackFrameStride = 60;
        private const int IndirectArgsInstanceCountIndex = 1;
        private const int MissingRegistryRefreshStrideFrames = 120;
        private const float DefaultFallbackAspect = 1.7777778f;
        private const float CullingHysteresisMeters = 5f;
        private const float CullingHysteresisSeconds = 2f;
        private const uint BlackBoxMagic = 0x47534C4Du;
        private const uint BlackBoxVersion = 1u;
        private const uint BlackBoxFlagGpuReady = 1u << 0;
        private const uint BlackBoxFlagCameraSignal = 1u << 1;
        private const uint BlackBoxFlagStressShed = 1u << 2;
        private const uint BlackBoxFlagHighTier = 1u << 3;
        private const uint BlackBoxFlagNonFiniteVaultMatrix = 1u << 4;
        private const uint BlackBoxDumpReasonNonFiniteMatrix = 0x4E414E31u;
        private const string GpuIndirectKeyword = "HECTON_GPU_INDIRECT";
        private const string QualityMx350Keyword = "_QUALITY_MX350";
        private const string QualityHighKeyword = "_QUALITY_HIGH";
        private const string ScatterFrameConstantsBufferName = "HectonScatterFrameConstants";
        private const int ScatterFrameConstantsStrideBytes = 176;

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
        private static readonly int _ShaderVisibleIndicesId = Shader.PropertyToID("_HectonVisibleInstanceIndices");
        private static readonly int _ShaderMotionVectorsId = Shader.PropertyToID("_HectonFloraMotionVectors");
        private static readonly int _GlobalFloatingOffsetId = Shader.PropertyToID("_GlobalFloatingOffset");
        private static readonly int _HectonFloatingOriginOffsetId = Shader.PropertyToID("_HectonFloatingOriginOffset");
        private static readonly int _LodNearDistanceId = Shader.PropertyToID("_HectonLodNearDistance");
        private static readonly int _LodFarDistanceId = Shader.PropertyToID("_HectonLodFarDistance");
        private static readonly int _LodTransitionRangeId = Shader.PropertyToID("_HectonLodTransitionRange");
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
        private GraphicsBuffer _argsBuffer;
        private GraphicsBuffer _frameConstantsBuffer;
        private MaterialPropertyBlock _materialProperties;
        private Plane[] _cameraPlanes;
        private Vector4[] _frustumPlaneUpload;
        private readonly ScatterFrameConstants[] _frameConstantsUpload = new ScatterFrameConstants[1]; // COLD ALLOC: ScatterFrameConstants[1] - packed compute constant upload lane - owner: GpuScatterLodManager
        private IDataVault _registryDataVault;
        private IDataVault _dataVault;
        private VaultBufferHandle<Matrix4x4> _vaultMatricesHandle;
        private VaultBufferHandle<GpuScatterFloraInstanceData> _vaultMetadataHandle;
        private VaultBufferHandle<ScatterBlackBoxEntry> _blackBoxHandle;
        private VaultBufferHandle<float4> _cpuFrustumPlanesHandle;
        private VaultBufferHandle<byte> _cpuVisibilityMaskHandle;
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
        private int _activeInstanceCount;
        private int _gpuBufferIndex;
        private int _scatterCullKernel = -1;
        private int _blackBoxCursor;
        private int _frameIndex;
        private int _lastVisibleFloraCount;
        private int _nextMissingRegistryRefreshFrame;
        private bool _registered;
        private bool _hotSwapRegistered;
        private bool _scalabilityEventsRegistered;
        private bool _originShiftListenerRegistered;
        private bool _gpuReady;
        private bool _forceUpload;
        private bool _metadataDefaultsInitialized;
        private bool _hasMatrixGeneration;
        private bool _hasMetadataGeneration;
        private bool _hasCameraSignal;
        private bool _hasExplicitDrawBounds;
        private bool _blackBoxDumped;
        private bool _pendingHighTier;
        private bool _cachedHighTier;
        private bool _tierCacheInitialized;
        private HectonQualityTier _pendingQualityTier;
        private HectonQualityTier _cachedQualityTier;
        private AsyncGPUReadbackRequest _visibleCountReadbackRequest;
        private bool _visibleCountReadbackPending;
        private Mesh _boundMesh;
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
            JobHandle handle = new ScatterCullJob
            {
                Matrices = matrices,
                CullingPlanes = frustumPlanes,
                VisibilityMask = visibilityMask,
                InstanceCount = _activeInstanceCount,
                AupShiftOffset = new float3(_aupShiftOffset.x, _aupShiftOffset.y, _aupShiftOffset.z),
                CameraPosition = new float3(_lastCameraSignalPosition.x, _lastCameraSignalPosition.y, _lastCameraSignalPosition.z),
                LocalBoundsCenter = new float3(localBoundsCenter.x, localBoundsCenter.y, localBoundsCenter.z),
                LocalBoundsExtents = new float3(localBoundsExtents.x, localBoundsExtents.y, localBoundsExtents.z),
                MaxDistanceSq = _effectiveCullDistanceMeters * _effectiveCullDistanceMeters
            }.Schedule(_activeInstanceCount, ThreadGroupSize);

            handle.Complete(); // COLD SYNC JOB: explicit manual audit, never part of the shipping Tick path.
            return true;
        }

        private void Awake()
        {
            _matrixBuffers = new GraphicsBuffer[DoubleBufferCount]; // COLD ALLOC: GraphicsBuffer[2] - double-buffered matrix upload handles - owner: GpuScatterLodManager
            _metadataBuffers = new GraphicsBuffer[DoubleBufferCount]; // COLD ALLOC: GraphicsBuffer[2] - double-buffered flora metadata upload handles - owner: GpuScatterLodManager
            _cameraPlanes = new Plane[FrustumPlaneCount]; // COLD ALLOC: Plane[6] - camera frustum cache - owner: GpuScatterLodManager
            _frustumPlaneUpload = new Vector4[FrustumPlaneCount]; // COLD ALLOC: Vector4[6] - compute frustum upload cache - owner: GpuScatterLodManager
            _materialProperties = new MaterialPropertyBlock(); // COLD ALLOC: MaterialPropertyBlock[1] - per-draw indirect flora shader state - owner: GpuScatterLodManager
            instanceCapacity = math.max(1, instanceCapacity);
            _activeInstanceCount = math.clamp(initialActiveInstanceCount, 0, instanceCapacity);
            _drawBounds = fallbackDrawBounds;
            _effectiveCullDistanceMeters = math.max(1f, lowTierCullDistanceMeters);
            _pendingCullDistanceMeters = _effectiveCullDistanceMeters;
            RefreshAupOffsetCold();
        }

        private void OnEnable()
        {
            RefreshAupOffsetCold();
            TryRegisterHotSwapListener();
            TryRegisterScalabilityEvents();
            TryRegisterOriginShiftListener();
            TryRegisterTick();
            _forceUpload = true;
        }

        private void OnDisable()
        {
            TryUnregisterTick();
            TryUnregisterOriginShiftListener();
            TryUnregisterScalabilityEvents();
            TryUnregisterHotSwapListener();
            ReleaseGpuBuffers();
            ReleaseCpuAuditBuffers();
            InvalidateDataVaultLease();
            _gpuReady = false;
        }

        private void OnDestroy()
        {
            ReleaseGpuBuffers();
            ReleaseCpuAuditBuffers();
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            if (!TryEnsureGpuState())
                return;

            ConsumeCameraFrustumSignals();
            ConsumeSystemHealthSignals();
            UpdateCullDistance(deltaTime);
            if (!TryBuildFrustumPlanes())
                return;

            int activeCount = ResolveSafeActiveCount();
            if (activeCount <= 0)
            {
                RecordBlackBox(0u, activeCount);
                return;
            }

            if (!TryUploadVaultBuffers(activeCount))
            {
                RecordBlackBox(BlackBoxFlagNonFiniteVaultMatrix, activeCount);
                return;
            }

            DispatchCull(activeCount);
            UpdateVisibleCountReadback(_frameIndex);
            Render(activeCount);
            RecordBlackBox(BuildRuntimeFlags(), activeCount);
            _frameIndex++;
        }

        /// <inheritdoc />
        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            _aupShiftOffset = ToVector3(shiftData.NewTotalOffsetDouble);
            if (_hasExplicitDrawBounds)
                _drawBounds.center -= shiftData.ShiftOffset;
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

        /// <inheritdoc />
        void IScalabilityChangedEventListener.OnScalabilityChanged(in ScalabilityChangedEvent payload)
        {
            _pendingQualityTier = payload.CurrentQualityTier;
            _pendingHighTier = IsHighTier(payload.CurrentQualityTier);
        }

        private void TryRegisterTick()
        {
            if (_registered)
                return;

            _registered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
        }

        private void TryUnregisterTick()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            _registered = false;
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

        private void TryRegisterScalabilityEvents()
        {
            if (!_scalabilityEventsRegistered)
            {
                ScalabilityEvents.Register(this);
                _scalabilityEventsRegistered = true;
            }

            if (!_tierCacheInitialized)
            {
                HectonQualityTier tier = GlobalRegistry.ScalabilityTier;
                _cachedQualityTier = tier;
                _pendingQualityTier = tier;
                _cachedHighTier = IsHighTier(tier);
                _pendingHighTier = _cachedHighTier;
                _tierCacheInitialized = true;
            }
        }

        private void TryUnregisterScalabilityEvents()
        {
            if (!_scalabilityEventsRegistered)
                return;

            ScalabilityEvents.Unregister(this);
            _scalabilityEventsRegistered = false;
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
            _registryDataVault = GlobalRegistry.DataVault;
            _nextMissingRegistryRefreshFrame = Time.frameCount + MissingRegistryRefreshStrideFrames;
        }

        private void RefreshMissingRegistryServicesIfNeeded()
        {
            if (_registryDataVault != null)
                return;

            int frame = Time.frameCount;
            if (frame < _nextMissingRegistryRefreshFrame)
                return;

            RefreshCachedRegistryServices();
        }

        private void ApplyRegistryServiceRebind(GlobalRegistryServiceSlot serviceSlot, object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.DataVault)
                return;

            IDataVault currentVault = currentService as IDataVault;
            _registryDataVault = currentVault;
            if (!ReferenceEquals(_dataVault, currentVault))
            {
                InvalidateDataVaultLease();
                _gpuReady = false;
            }
        }

        private bool TryEnsureGpuState()
        {
            if (_gpuReady && IsGpuStateValid())
                return true;

            _gpuReady = false;
            if (scatterCullCompute == null || floraMesh == null || floraMaterial == null)
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

            if (!_vaultMatricesHandle.IsCreated || !_vaultMetadataHandle.IsCreated)
                return false;

            if (!vault.ResolveBuffer(ref _vaultMatricesHandle) || !vault.ResolveBuffer(ref _vaultMetadataHandle))
                return false;

            _scatterCullKernel = ResolveKernel(scatterCullCompute, "ScatterCullJob");
            if (_scatterCullKernel < 0)
                return false;

            EnsureGpuBuffers();
            InitializeIndirectArgs(floraMesh);
            _gpuReady = IsGpuStateValid();
            return _gpuReady;
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
                   _argsBuffer != null &&
                   _scatterCullKernel >= 0;
        }

        private void BindDataVault(IDataVault vault)
        {
            _dataVault = vault;
            _vaultMatricesHandle = vault.GetBufferHandle<Matrix4x4>(
                BufferID.FloraScatterMatrices,
                instanceCapacity,
                SystemID.Vfx,
                NativeArrayOptions.UninitializedMemory);
            _vaultMetadataHandle = vault.GetBufferHandle<GpuScatterFloraInstanceData>(
                BufferID.FloraScatterMetadata,
                instanceCapacity,
                SystemID.Vfx,
                NativeArrayOptions.ClearMemory);
            EnsureBlackBox(vault);
            CaptureVaultGenerations(vault);
            EnsureMetadataDefaults();
            _forceUpload = true;
        }

        private void InvalidateDataVaultLease()
        {
            _dataVault = null;
            _vaultMatricesHandle = default;
            _vaultMetadataHandle = default;
            _blackBoxHandle = default;
            _cpuFrustumPlanesHandle = default;
            _cpuVisibilityMaskHandle = default;
            _hasMatrixGeneration = false;
            _hasMetadataGeneration = false;
            _lastMatrixGeneration = 0u;
            _lastMetadataGeneration = 0u;
            _metadataDefaultsInitialized = false;
            _forceUpload = true;
        }

        private void EnsureGpuBuffers()
        {
            for (int i = 0; i < DoubleBufferCount; i++)
            {
                if (_matrixBuffers[i] == null || _matrixBuffers[i].count < instanceCapacity)
                    RecreateStructuredLockBuffer(ref _matrixBuffers[i], instanceCapacity, Matrix4x4StrideBytes);
                if (_metadataBuffers[i] == null || _metadataBuffers[i].count < instanceCapacity)
                    RecreateStructuredLockBuffer(ref _metadataBuffers[i], instanceCapacity, GpuScatterFloraInstanceData.Stride);
            }

            if (_visibleIndexBuffer == null || _visibleIndexBuffer.count < instanceCapacity)
                RecreateAppendBuffer(ref _visibleIndexBuffer, instanceCapacity, sizeof(uint));
            if (_visibleMatrixBuffer == null || _visibleMatrixBuffer.count < instanceCapacity)
                RecreateAppendBuffer(ref _visibleMatrixBuffer, instanceCapacity, Matrix4x4StrideBytes);
            if (_motionVectorBuffer == null || _motionVectorBuffer.count < instanceCapacity)
                RecreateStructuredBuffer(ref _motionVectorBuffer, instanceCapacity, UnsafeSizeOfVector4());
            if (_argsBuffer == null)
            {
                _argsBuffer = new GraphicsBuffer(
                    GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Raw,
                    1,
                    GraphicsBuffer.IndirectDrawIndexedArgs.size); // COLD ALLOC: GraphicsBuffer[1] - indirect flora draw args - owner: GpuScatterLodManager
            }

            if (SystemInfo.supportsSetConstantBuffer &&
                (_frameConstantsBuffer == null || !_frameConstantsBuffer.IsValid()))
            {
                _frameConstantsBuffer = new GraphicsBuffer(
                    GraphicsBuffer.Target.Constant,
                    GraphicsBuffer.UsageFlags.LockBufferForWrite,
                    1,
                    ScatterFrameConstantsStrideBytes); // COLD ALLOC: GraphicsBuffer[80B] - packed scatter compute constants - owner: GpuScatterLodManager
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
            ReleaseBuffer(ref _argsBuffer);
            ReleaseBuffer(ref _frameConstantsBuffer);
            _visibleCountReadbackPending = false;
            _gpuReady = false;
        }

        private static void ReleaseBuffer(ref GraphicsBuffer buffer)
        {
            if (buffer == null)
                return;

            buffer.Release();
            buffer = null;
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

            var argsWrite = _argsBuffer.LockBufferForWrite<GraphicsBuffer.IndirectDrawIndexedArgs>(0, 1);
            argsWrite[0] = new GraphicsBuffer.IndirectDrawIndexedArgs
            {
                indexCountPerInstance = indexCount,
                instanceCount = 0u,
                startIndex = startIndex,
                baseVertexIndex = baseVertex,
                startInstance = 0u
            };
            _argsBuffer.UnlockBufferAfterWrite<GraphicsBuffer.IndirectDrawIndexedArgs>(1);

            _boundMesh = mesh;
            _boundIndexCount = indexCount;
            _boundStartIndex = startIndex;
            _boundBaseVertex = baseVertex;
        }

        private int ResolveSafeActiveCount()
        {
            if (!_vaultMatricesHandle.IsCreated)
                return 0;

            int safeCount = math.min(_activeInstanceCount, _vaultMatricesHandle.Length);
            safeCount = _vaultMetadataHandle.IsCreated ? math.min(safeCount, _vaultMetadataHandle.Length) : safeCount;
            return math.clamp(safeCount, 0, instanceCapacity);
        }

        private bool TryUploadVaultBuffers(int activeCount)
        {
            IDataVault vault = _dataVault;
            if (vault == null || !_vaultMatricesHandle.IsCreated || !_vaultMetadataHandle.IsCreated)
                return false;

            bool generationChanged = HasVaultGenerationChanged(vault);
            if (!_forceUpload && !generationChanged)
                return true;

            if (!TryResolveMatrixView(out var matrices) ||
                !TryResolveMetadataView(out var metadata))
            {
                return false;
            }

            if (!ValidateFiniteMatrices(matrices, activeCount))
                return false;

            int writeIndex = 1 - _gpuBufferIndex;
            GraphicsBufferUploadUtility.UploadNativeArray(_matrixBuffers[writeIndex], matrices, activeCount);
            GraphicsBufferUploadUtility.UploadNativeArray(_metadataBuffers[writeIndex], metadata, activeCount);
            _gpuBufferIndex = writeIndex;
            CaptureVaultGenerations(vault);
            _forceUpload = false;
            return true;
        }

        private bool HasVaultGenerationChanged(IDataVault vault)
        {
            bool matrixGenerationFound = vault.TryGetBufferGeneration(BufferID.FloraScatterMatrices, out uint matrixGeneration);
            bool metadataGenerationFound = vault.TryGetBufferGeneration(BufferID.FloraScatterMetadata, out uint metadataGeneration);
            bool changed = (!_hasMatrixGeneration && matrixGenerationFound) ||
                           (!_hasMetadataGeneration && metadataGenerationFound) ||
                           (_hasMatrixGeneration && matrixGenerationFound && matrixGeneration != _lastMatrixGeneration) ||
                           (_hasMetadataGeneration && metadataGenerationFound && metadataGeneration != _lastMetadataGeneration);
            return changed;
        }

        private void CaptureVaultGenerations(IDataVault vault)
        {
            _hasMatrixGeneration = vault.TryGetBufferGeneration(BufferID.FloraScatterMatrices, out _lastMatrixGeneration);
            _hasMetadataGeneration = vault.TryGetBufferGeneration(BufferID.FloraScatterMetadata, out _lastMetadataGeneration);
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
            _visibleIndexBuffer.SetCounterValue(0u);
            _visibleMatrixBuffer.SetCounterValue(0u);

            GraphicsBuffer matrixBuffer = _matrixBuffers[_gpuBufferIndex];
            scatterCullCompute.SetBuffer(_scatterCullKernel, _SourceMatricesId, matrixBuffer);
            scatterCullCompute.SetBuffer(_scatterCullKernel, _VisibleIndicesId, _visibleIndexBuffer);
            scatterCullCompute.SetBuffer(_scatterCullKernel, _VisibleMatricesId, _visibleMatrixBuffer);
            scatterCullCompute.SetBuffer(_scatterCullKernel, _MotionVectorsId, _motionVectorBuffer);
            scatterCullCompute.SetVectorArray(_FrustumPlanesId, _frustumPlaneUpload);
            float cullDistance = math.max(1f, _effectiveCullDistanceMeters);
            UploadScatterFrameConstants(activeCount, cullDistance * cullDistance, ResolveSafeLocalBoundsExtents());
            scatterCullCompute.Dispatch(_scatterCullKernel, math.max(1, (activeCount + ThreadGroupSize - 1) / ThreadGroupSize), 1, 1);
            GraphicsBuffer.CopyCount(_visibleMatrixBuffer, _argsBuffer, sizeof(uint));
        }

        private void UploadScatterFrameConstants(int activeCount, float maxDistanceSq, Vector3 safeLocalBoundsExtents)
        {
            ScatterFrameConstants constants = new ScatterFrameConstants
            {
                Params0 = new Vector4(
                    math.max(0, activeCount),
                    math.max(1f, maxDistanceSq),
                    math.max(0f, swayMotionStrength),
                    _frameIndex & 0x00FFFFFF),
                Params1 = new Vector4(_aupShiftOffset.x, _aupShiftOffset.y, _aupShiftOffset.z, _cachedHighTier ? 1f : 0f),
                Params2 = new Vector4(_lastCameraSignalPosition.x, _lastCameraSignalPosition.y, _lastCameraSignalPosition.z, 0f),
                Params3 = new Vector4(localBoundsCenter.x, localBoundsCenter.y, localBoundsCenter.z, 0f),
                Params4 = new Vector4(safeLocalBoundsExtents.x, safeLocalBoundsExtents.y, safeLocalBoundsExtents.z, 0f)
            };

            if (SystemInfo.supportsSetConstantBuffer &&
                _frameConstantsBuffer != null &&
                _frameConstantsBuffer.IsValid())
            {
                _frameConstantsUpload[0] = constants;
                GraphicsBufferUploadUtility.UploadArray(_frameConstantsBuffer, _frameConstantsUpload, 1);
                scatterCullCompute.SetConstantBuffer(ScatterFrameConstantsBufferName, _frameConstantsBuffer, 0, ScatterFrameConstantsStrideBytes);
                return;
            }

            scatterCullCompute.SetVector(_ScatterParams0Id, constants.Params0);
            scatterCullCompute.SetVector(_ScatterParams1Id, constants.Params1);
            scatterCullCompute.SetVector(_ScatterParams2Id, constants.Params2);
            scatterCullCompute.SetVector(_ScatterParams3Id, constants.Params3);
            scatterCullCompute.SetVector(_ScatterParams4Id, constants.Params4);
        }

        private void Render(int activeCount)
        {
            Material material = floraMaterial;
            Mesh mesh = floraMesh;
            if (material == null || mesh == null || activeCount <= 0)
                return;

            material.enableInstancing = true;
            material.EnableKeyword(GpuIndirectKeyword);
            MaterialPropertyBlock properties = _materialProperties;
            if (properties == null)
                return;

            properties.Clear();
            properties.SetBuffer(_ShaderInstanceMatricesId, _matrixBuffers[_gpuBufferIndex]);
            properties.SetBuffer(_ShaderInstanceDataId, _metadataBuffers[_gpuBufferIndex]);
            properties.SetBuffer(_ShaderVisibleIndicesId, _visibleIndexBuffer);
            properties.SetBuffer(_VisibleMatricesId, _visibleMatrixBuffer);
            properties.SetBuffer(_ShaderMotionVectorsId, _motionVectorBuffer);
            properties.SetVector(_GlobalFloatingOffsetId, _aupShiftOffset);
            properties.SetVector(_HectonFloatingOriginOffsetId, _aupShiftOffset);
            properties.SetFloat(_LodNearDistanceId, math.max(1f, lowTierCullDistanceMeters));
            properties.SetFloat(_LodFarDistanceId, math.max(1f, _effectiveCullDistanceMeters));
            properties.SetFloat(_LodTransitionRangeId, _cachedHighTier ? math.max(0f, lodCrossfadeRangeMeters) : 0f);
            ApplyMaterialScalability(material, properties);

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

            Graphics.RenderMeshIndirect(renderParams, mesh, _argsBuffer, 1, 0);
        }

        private void ApplyMaterialScalability(Material material, MaterialPropertyBlock properties)
        {
            if (_cachedHighTier)
            {
                material.EnableKeyword(QualityHighKeyword);
                material.DisableKeyword(QualityMx350Keyword);
                properties.SetFloat(_AnisotropicSssStrengthId, math.max(0f, highTierAnisotropicSssStrength));
                properties.SetFloat(_OrganicSssScaleId, math.max(0f, highTierOrganicSssScale));
                properties.SetFloat(_EdgeBloomStrengthId, math.max(0f, highTierEdgeBloomStrength));
                properties.SetFloat(_LocalCausticStrengthId, math.max(0f, highTierLocalCausticStrength));
                return;
            }

            material.EnableKeyword(QualityMx350Keyword);
            material.DisableKeyword(QualityHighKeyword);
            properties.SetFloat(_AnisotropicSssStrengthId, math.max(0f, lowTierAnisotropicSssStrength));
            properties.SetFloat(_OrganicSssScaleId, math.max(0f, lowTierOrganicSssScale));
            properties.SetFloat(_EdgeBloomStrengthId, math.max(0f, lowTierEdgeBloomStrength));
            properties.SetFloat(_LocalCausticStrengthId, math.max(0f, lowTierLocalCausticStrength));
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
                    var argsData = _visibleCountReadbackRequest.GetData<uint>();
                    _lastVisibleFloraCount = argsData.Length > IndirectArgsInstanceCountIndex
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
                return true;
            }

            if (!_hasCameraSignal)
                return false;

            BuildFallbackFrustumPlanesFromSignal();
            return true;
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

            float nearClip = math.max(0.01f, _lastCameraSignalNearMeters);
            float farClip = math.max(nearClip + 1f, math.min(_lastCameraSignalFarMeters, _effectiveCullDistanceMeters));
            float verticalTan = math.tan(math.radians(math.clamp(_lastCameraSignalFovDegrees, 5f, 160f) * 0.5f));
            float nearHalfY = verticalTan * nearClip;
            float nearHalfX = nearHalfY * math.max(0.25f, fallbackAspect);

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

        private void UpdateCullDistance(float deltaTime)
        {
            if (_pendingHighTier != _cachedHighTier || _pendingQualityTier != _cachedQualityTier)
            {
                _cullDistanceHysteresisTimer += math.max(0f, deltaTime);
                if (_cullDistanceHysteresisTimer >= CullingHysteresisSeconds)
                {
                    _cachedQualityTier = _pendingQualityTier;
                    _cachedHighTier = _pendingHighTier;
                    _cullDistanceHysteresisTimer = 0f;
                }
            }

            float desired = ResolveDesiredCullDistance();
            if (_systemStress01 > 0.8f)
                desired *= 0.5f;

            desired = math.max(1f, desired);
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

            _cullDistanceHysteresisTimer += math.max(0f, deltaTime);
            if (_cullDistanceHysteresisTimer >= CullingHysteresisSeconds)
            {
                _effectiveCullDistanceMeters = desired;
                _cullDistanceHysteresisTimer = 0f;
            }
        }

        private float ResolveDesiredCullDistance()
        {
            if (_cachedHighTier)
                return math.max(1f, highTierCullDistanceMeters);

            if (_cachedQualityTier == HectonQualityTier.Mid)
                return math.max(1f, midTierCullDistanceMeters);

            return math.max(1f, lowTierCullDistanceMeters);
        }

        private Bounds ResolveFallbackDrawBounds()
        {
            float diameter = math.max(2f, _effectiveCullDistanceMeters * 2f);
            float height = math.max(8f, localBoundsExtents.y * 4f);
            if (IsFiniteBounds(fallbackDrawBounds))
            {
                Vector3 fallbackSize = fallbackDrawBounds.size;
                diameter = math.max(diameter, math.max(fallbackSize.x, fallbackSize.z));
                height = math.max(height, fallbackSize.y);
            }

            return new Bounds(_lastCameraSignalPosition, new Vector3(diameter, height, diameter));
        }

        private Vector3 ResolveSafeLocalBoundsExtents()
        {
            return new Vector3(
                math.max(0.01f, math.abs(localBoundsExtents.x)),
                math.max(0.01f, math.abs(localBoundsExtents.y)),
                math.max(0.01f, math.abs(localBoundsExtents.z)));
        }

        private bool TryResolveMatrixView(out NativeArray<Matrix4x4> matrices)
        {
            IDataVault vault = _dataVault;
            matrices = vault != null ? _vaultMatricesHandle.Resolve(vault) : default;
            return matrices.IsCreated;
        }

        private bool TryResolveMetadataView(out NativeArray<GpuScatterFloraInstanceData> metadata)
        {
            IDataVault vault = _dataVault;
            metadata = vault != null ? _vaultMetadataHandle.Resolve(vault) : default;
            return metadata.IsCreated;
        }

        private bool TryResolveCpuFrustumPlaneView(out NativeArray<float4> frustumPlanes)
        {
            IDataVault vault = _dataVault;
            frustumPlanes = vault != null ? _cpuFrustumPlanesHandle.Resolve(vault) : default;
            return frustumPlanes.IsCreated;
        }

        private bool TryResolveCpuVisibilityMaskView(out NativeArray<byte> visibilityMask)
        {
            IDataVault vault = _dataVault;
            visibilityMask = vault != null ? _cpuVisibilityMaskHandle.Resolve(vault) : default;
            return visibilityMask.IsCreated;
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

        private bool EnsureBlackBox(IDataVault vault)
        {
            if (vault == null)
                return false;

            if (_blackBoxHandle.IsCreated && vault.ResolveBuffer(ref _blackBoxHandle))
                return _blackBoxHandle.Length >= TelemetryCapacity;

            _blackBoxHandle = vault.GetBufferHandle<ScatterBlackBoxEntry>(
                BufferID.FloraScatterBlackBox,
                TelemetryCapacity,
                SystemID.Vfx,
                NativeArrayOptions.ClearMemory);
            _blackBoxCursor = 0;
            return _blackBoxHandle.IsCreated && _blackBoxHandle.Length >= TelemetryCapacity;
        }

        private void RecordBlackBox(uint flags, int activeCount)
        {
            if (!TryResolveBlackBoxPointer(out ScatterBlackBoxEntry* blackBox, out int blackBoxLength))
                return;

            flags |= _gpuReady ? BlackBoxFlagGpuReady : 0u;
            flags |= _hasCameraSignal ? BlackBoxFlagCameraSignal : 0u;
            flags |= _systemStress01 > 0.8f ? BlackBoxFlagStressShed : 0u;
            flags |= _cachedHighTier ? BlackBoxFlagHighTier : 0u;

            int index = _blackBoxCursor % blackBoxLength;
            blackBox[index] = new ScatterBlackBoxEntry
            {
                Frame = Time.frameCount,
                ActiveInstanceCount = activeCount,
                VisibleFloraCount = _lastVisibleFloraCount,
                CullDistanceMeters = _effectiveCullDistanceMeters,
                SystemStress01 = _systemStress01,
                CameraPosition = new float3(_lastCameraSignalPosition.x, _lastCameraSignalPosition.y, _lastCameraSignalPosition.z),
                AupShiftOffset = new float3(_aupShiftOffset.x, _aupShiftOffset.y, _aupShiftOffset.z),
                MatrixGeneration = _lastMatrixGeneration,
                MetadataGeneration = _lastMetadataGeneration,
                Flags = flags,
                Reserved0 = 0u,
                Reserved1 = 0u
            };
            _blackBoxCursor = (_blackBoxCursor + 1) % blackBoxLength;
        }

        private uint BuildRuntimeFlags()
        {
            uint flags = 0u;
            flags |= _gpuReady ? BlackBoxFlagGpuReady : 0u;
            flags |= _hasCameraSignal ? BlackBoxFlagCameraSignal : 0u;
            flags |= _systemStress01 > 0.8f ? BlackBoxFlagStressShed : 0u;
            flags |= _cachedHighTier ? BlackBoxFlagHighTier : 0u;
            return flags;
        }

        private void DumpBlackBox(uint reason)
        {
            if (_blackBoxDumped || !TryResolveBlackBoxPointer(out ScatterBlackBoxEntry* blackBox, out int blackBoxLength))
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
                    writer.Write(entry.Reserved0);
                    writer.Write(entry.Reserved1);
                }
            }
            catch (Exception)
            {
                GlobalTelemetryBus.PublishMathGuardInvalidNumber(unchecked((int)reason));
            }
        }

        private bool TryResolveBlackBoxPointer(out ScatterBlackBoxEntry* blackBox, out int length)
        {
            blackBox = null;
            length = 0;
            IDataVault vault = _dataVault;
            if (!EnsureBlackBox(vault))
                return false;

            void* pointer = _blackBoxHandle.ResolvePointer(vault);
            if (pointer == null || _blackBoxHandle.Length <= 0)
                return false;

            blackBox = (ScatterBlackBoxEntry*)pointer;
            length = _blackBoxHandle.Length;
            return true;
        }

        private bool EnsureCpuAuditBuffers(int activeCount)
        {
            if (!enableBurstCullAudit)
                return false;

            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            if (!_cpuFrustumPlanesHandle.IsCreated || !vault.ResolveBuffer(ref _cpuFrustumPlanesHandle))
            {
                _cpuFrustumPlanesHandle = vault.GetBufferHandle<float4>(
                    BufferID.FloraScatterCpuFrustumPlanes,
                    FrustumPlaneCount,
                    SystemID.Vfx,
                    NativeArrayOptions.UninitializedMemory);
            }

            int visibilityCapacity = math.max(activeCount, instanceCapacity);
            bool needsVisibilityBuffer =
                !_cpuVisibilityMaskHandle.IsCreated ||
                _cpuVisibilityMaskHandle.Length < visibilityCapacity ||
                !vault.ResolveBuffer(ref _cpuVisibilityMaskHandle);
            if (needsVisibilityBuffer)
            {
                _cpuVisibilityMaskHandle = vault.GetBufferHandle<byte>(
                    BufferID.FloraScatterCpuVisibilityMask,
                    visibilityCapacity,
                    SystemID.Vfx,
                    NativeArrayOptions.UninitializedMemory);
            }

            return _cpuFrustumPlanesHandle.IsCreated && _cpuVisibilityMaskHandle.IsCreated;
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

        private static int ResolveKernel(ComputeShader compute, string kernelName)
        {
            if (compute == null || !compute.HasKernel(kernelName))
                return -1;

            return compute.FindKernel(kernelName);
        }

        private static bool IsHighTier(HectonQualityTier tier)
        {
            return tier == HectonQualityTier.High || tier == HectonQualityTier.Ultra;
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

        private static string ResolveAgentLogPath(string fileName)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Directory.GetCurrentDirectory();
            return Path.Combine(projectRoot, "Docs", "AgentLogs", fileName);
        }

        private void RefreshAupOffsetCold()
        {
            _aupShiftOffset = ToVector3(HectonFloatingOrigin.CurrentTotalOffsetDouble);
        }

        private static Vector3 ToVector3(double3 value)
        {
            return new Vector3((float)value.x, (float)value.y, (float)value.z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int UnsafeSizeOfVector4()
        {
            return 16;
        }

        private static int Matrix4x4StrideBytes => 64;

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = ScatterFrameConstantsStrideBytes)]
        private struct ScatterFrameConstants
        {
            public Vector4 Params0;
            public Vector4 Params1;
            public Vector4 Params2;
            public Vector4 Params3;
            public Vector4 Params4;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]
        private struct ScatterBlackBoxEntry
        {
            public int Frame;
            public int ActiveInstanceCount;
            public int VisibleFloraCount;
            public float CullDistanceMeters;
            public float SystemStress01;
            public float3 CameraPosition;
            public float3 AupShiftOffset;
            public uint MatrixGeneration;
            public uint MetadataGeneration;
            public uint Flags;
            public uint Reserved0;
            public uint Reserved1;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct ScatterCullJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<Matrix4x4> Matrices;
            [ReadOnly] public NativeArray<float4> CullingPlanes;
            [WriteOnly] public NativeArray<byte> VisibilityMask;
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
                if (!HasUsableScale(matrix))
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
                if (!math.isfinite(distanceSq) || distanceSq > MaxDistanceSq || !BoundsVisible(matrix, center))
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
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            instanceCapacity = math.max(1, instanceCapacity);
            initialActiveInstanceCount = math.clamp(initialActiveInstanceCount, 0, instanceCapacity);
            lowTierCullDistanceMeters = math.max(1f, lowTierCullDistanceMeters);
            midTierCullDistanceMeters = math.max(lowTierCullDistanceMeters, midTierCullDistanceMeters);
            highTierCullDistanceMeters = math.max(midTierCullDistanceMeters, highTierCullDistanceMeters);
            fallbackAspect = math.max(0.25f, fallbackAspect);
            localBoundsExtents = new Vector3(
                math.max(0.01f, math.abs(localBoundsExtents.x)),
                math.max(0.01f, math.abs(localBoundsExtents.y)),
                math.max(0.01f, math.abs(localBoundsExtents.z)));
        }
#endif
    }
}
