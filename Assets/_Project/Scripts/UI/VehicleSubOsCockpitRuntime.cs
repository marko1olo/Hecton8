using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Gameplay;
using Hecton8.Power;
using Hecton8.World;
using TMPro;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.UI
{
    /// <summary>
    /// Dispatcher-owned diegetic submarine cockpit bridge: analytical controls, off-screen screens, and GPU sonar radar.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VehicleSubOsCockpitRuntime : MonoBehaviour, ILateFrameTickable, IRenderable, ISubmarineOsEventListener, IPowerGridTelemetryListener, IGlobalRegistryHotSwapListener
    {
        private const int MaxRadarPoints = 4096;
        private const int MinQualityRadarPoints = 512;
        private const int MaxRadarPointsPerTap = 256;
        private const int MinQualityRadarPointsPerTap = 32;
        private const float CheapVisualQualityThreshold = 0.3f;
        private const float CheapVisualQualityRampInv = 5.5555553f;
        private const float ExternalFeedEnableThreshold = 0.18f;
        private const float RadarCapacityQuantumInv = 0.0078125f;
        private const float RadarPointTapQuantumInv = 0.0625f;
        private const int MaxButtons = 32;
        private const int MaxDamageHologramPoints = 512;
        private const int FallbackDamageWarningPoints = 7;
        private const int MaxDamageHologramRooms = 32;
        private const int MinDamageProxyVertices = 8;
        private const float DamageHologramFlickerSeconds = 0.5f;
        private const float DamageHologramFlickerSecondsInv = 2f;
        private const float Hash24Inv = 5.9604648e-8f;
        private const int MinUiRenderTextureWidth = 256;
        private const int MinUiRenderTextureHeight = 128;
        private const int MinQualityUiRenderTextureMaxWidth = 512;
        private const int MinQualityUiRenderTextureMaxHeight = 256;
        private const int MinExternalRenderTextureWidth = 256;
        private const int MinExternalRenderTextureHeight = 144;
        private const int TelemetryCapacity = 300;
        private const int TextBufferCapacity = 96;
        private const float ButtonTravelSeconds = 0.1f;
        private const float ButtonTravelSecondsInv = 10f;
        private const float RadarPowerCutoff = 0.2f;
        private const float RadarRedispatchPowerEpsilon = 0.01f;
        private const float RadarRedispatchFlickerEpsilon = 0.01f;
        private const float DefaultMaxSonarDelaySeconds = 6.75f;
        private const string ComputeAssetPath = "Assets/_Project/Art/Shaders/Hecton_CockpitHoloRadar.compute";
        private const string DamageHologramComputeAssetPath = "Assets/_Project/Art/Shaders/Hecton_DamageHologram.compute";
        private const string DamageHologramMaterialAssetPath = "Assets/_Project/Art/Materials/MAT_Damage_Hologram.mat";
        private const string DamageHologramKernelName = "KMapHullDents";
        private const uint PortableMaxComputeThreadsPerGroup = 256u;
        private const uint TelemetryContextHash = 0x56534F53u; // VSOS
        private const uint DamageHologramTelemetryHash = 0x44484F4Cu; // DHOL
        private const uint RadarActiveHash = 0x52414452u; // RADR
        private const uint InteractionHash = 0x42544E53u; // BTNS
        private const int InvalidDisplayBucket = int.MinValue;
        private const int StatusModeInternalBus = 0;
        private const int StatusModeExternalLive = 1;
        private const int StatusModeExternalStatic = 2;
        private const int StatusModeExternalLocked = 3;
        private const SystemID VaultOwnerSystemId = SystemID.UI;
        private const BufferID ButtonStatesBufferId = BufferID.VehicleSubOsButtonStates;
        private const BufferID ButtonTargetsBufferId = BufferID.VehicleSubOsButtonTargets;
        private const BufferID ButtonProgressBufferId = BufferID.VehicleSubOsButtonProgress;
        private const BufferID ButtonOffsetsBufferId = BufferID.VehicleSubOsButtonOffsets;
        private const BufferID ButtonBaseLocalPositionsBufferId = BufferID.VehicleSubOsButtonBaseLocalPositions;
        private const BufferID ButtonMatricesBufferId = BufferID.VehicleSubOsButtonMatrices;
        private const BufferID TelemetryRingBufferId = BufferID.VehicleSubOsTelemetryRing;

        private static readonly int SonarTapsId = Shader.PropertyToID("_SonarEchoTaps");
        private static readonly int RadarBlipsId = Shader.PropertyToID("_RadarBlips");
        private static readonly int InputTapCountId = Shader.PropertyToID("_InputTapCount");
        private static readonly int OutputPointCountId = Shader.PropertyToID("_OutputPointCount");
        private static readonly int OutputCapacityId = Shader.PropertyToID("_OutputCapacity");
        private static readonly int SequenceId = Shader.PropertyToID("_Sequence");
        private static readonly int RadarRadiusMetersId = Shader.PropertyToID("_RadarRadiusMeters");
        private static readonly int MaxDelaySecondsId = Shader.PropertyToID("_MaxDelaySeconds");
        private static readonly int PowerLevelId = Shader.PropertyToID("_PowerLevel");
        private static readonly int DamageFlickerId = Shader.PropertyToID("_DamageFlicker");
        private static readonly int HectonRadarBlipsId = Shader.PropertyToID("_HectonRadarBlips");
        private static readonly int HectonGroundRadarPingsId = Shader.PropertyToID("_HectonGroundRadarPings");
        private static readonly int HectonRadarLocalToWorldId = Shader.PropertyToID("_HectonRadarLocalToWorld");
        private static readonly int HectonRadarProceduralId = Shader.PropertyToID("_HectonRadarProcedural");
        private static readonly int HectonRadarGprProceduralId = Shader.PropertyToID("_HectonRadarGprProcedural");
        private static readonly int HectonRadarGprOriginRadiusId = Shader.PropertyToID("_HectonRadarGprOriginRadius");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int PanelPowerLevelId = Shader.PropertyToID("_PanelPowerLevel");
        private static readonly int ExternalFeedBlendId = Shader.PropertyToID("_ExternalFeedBlend");
        private static readonly int DamageProxyVerticesId = Shader.PropertyToID("_HectonDamageProxyVertices");
        private static readonly int DamageHologramPointsId = Shader.PropertyToID("_HectonDamageHologramPoints");
        private static readonly int DamageRoomWaterLevelsId = Shader.PropertyToID("_HectonDamageRoomWaterLevels");
        private static readonly int DamageHologramLocalToWorldId = Shader.PropertyToID("_HectonDamageHologramLocalToWorld");
        private static readonly int DamageHologramParamsId = Shader.PropertyToID("_HectonDamageHologramParams");
        private static readonly int DamageHologramBoundsId = Shader.PropertyToID("_HectonDamageHologramBounds");
        private static readonly int DamageProxyVertexCountId = Shader.PropertyToID("_HectonDamageProxyVertexCount");
        private static readonly int DamageRoomCountId = Shader.PropertyToID("_HectonDamageRoomCount");
        private static readonly int DamageHologramFlickerId = Shader.PropertyToID("_Flicker");
        private static readonly Vector3[] RadarQuadVertices =
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3(0.5f, -0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f)
        }; // COLD ALLOC: Vector3[4] - immutable cockpit radar billboard quad vertices - owner: VehicleSubOsCockpitRuntime
        private static readonly Vector2[] RadarQuadUvs =
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f)
        }; // COLD ALLOC: Vector2[4] - immutable cockpit radar billboard quad UVs - owner: VehicleSubOsCockpitRuntime
        private static readonly int[] RadarQuadIndices =
        {
            0, 1, 2,
            0, 2, 3
        }; // COLD ALLOC: int[6] - immutable cockpit radar billboard quad indices - owner: VehicleSubOsCockpitRuntime
        private static readonly Vector3[] DamageCubeVertices =
        {
            new Vector3(-0.5f, -0.5f, -0.5f),
            new Vector3(0.5f, -0.5f, -0.5f),
            new Vector3(0.5f, 0.5f, -0.5f),
            new Vector3(-0.5f, 0.5f, -0.5f),
            new Vector3(-0.5f, -0.5f, 0.5f),
            new Vector3(0.5f, -0.5f, 0.5f),
            new Vector3(0.5f, 0.5f, 0.5f),
            new Vector3(-0.5f, 0.5f, 0.5f)
        }; // COLD ALLOC: Vector3[8] - immutable hologram cube vertices - owner: VehicleSubOsCockpitRuntime
        private static readonly int[] DamageCubeIndices =
        {
            0, 2, 1, 0, 3, 2,
            4, 5, 6, 4, 6, 7,
            0, 1, 5, 0, 5, 4,
            2, 3, 7, 2, 7, 6,
            1, 2, 6, 1, 6, 5,
            0, 4, 7, 0, 7, 3
        }; // COLD ALLOC: int[36] - immutable hologram cube indices - owner: VehicleSubOsCockpitRuntime
        private static readonly Vector3[] FallbackDamageProxyVertices =
        {
            new Vector3(-0.62f, -0.1f, -0.08f),
            new Vector3(-0.42f, 0.12f, -0.12f),
            new Vector3(-0.18f, 0.18f, -0.16f),
            new Vector3(0.18f, 0.18f, -0.16f),
            new Vector3(0.42f, 0.12f, -0.12f),
            new Vector3(0.62f, -0.1f, -0.08f),
            new Vector3(-0.62f, -0.1f, 0.08f),
            new Vector3(-0.42f, 0.12f, 0.12f),
            new Vector3(-0.18f, 0.18f, 0.16f),
            new Vector3(0.18f, 0.18f, 0.16f),
            new Vector3(0.42f, 0.12f, 0.12f),
            new Vector3(0.62f, -0.1f, 0.08f),
            new Vector3(-0.5f, -0.28f, -0.06f),
            new Vector3(-0.24f, -0.34f, -0.12f),
            new Vector3(0.24f, -0.34f, -0.12f),
            new Vector3(0.5f, -0.28f, -0.06f),
            new Vector3(-0.5f, -0.28f, 0.06f),
            new Vector3(-0.24f, -0.34f, 0.12f),
            new Vector3(0.24f, -0.34f, 0.12f),
            new Vector3(0.5f, -0.28f, 0.06f),
            new Vector3(-0.36f, 0.0f, -0.2f),
            new Vector3(0.0f, 0.05f, -0.24f),
            new Vector3(0.36f, 0.0f, -0.2f),
            new Vector3(-0.36f, 0.0f, 0.2f),
            new Vector3(0.0f, 0.05f, 0.24f),
            new Vector3(0.36f, 0.0f, 0.2f),
            new Vector3(-0.08f, 0.32f, 0.0f),
            new Vector3(0.08f, 0.32f, 0.0f),
            new Vector3(-0.08f, -0.42f, 0.0f),
            new Vector3(0.08f, -0.42f, 0.0f),
            new Vector3(-0.74f, -0.12f, 0.0f),
            new Vector3(0.74f, -0.12f, 0.0f)
        }; // COLD ALLOC: Vector3[32] - fallback coarse submarine proxy when LOD3 mesh is not wired - owner: VehicleSubOsCockpitRuntime

        [Header("Radar")]
        [SerializeField] private Transform radarDomeAnchor;
        [SerializeField] private ComputeShader radarCompute;
        [SerializeField] private Material radarBlipMaterial;
        [SerializeField] private Mesh radarBlipMesh;
        [SerializeField] private float radarRadiusMeters = 0.42f;
        [SerializeField] private float radarBoundsSizeMeters = 1.2f;
        [SerializeField] private int radarLayer;

        [Header("Damage Hologram")]
        [SerializeField] private Transform damageHologramAnchor;
        [SerializeField] private ComputeShader damageHologramCompute;
        [SerializeField] private Material damageHologramMaterial;
        [SerializeField] private Mesh damageProxyMeshLod3;
        [SerializeField] private Mesh damagePointMesh;
        [SerializeField] private float damageHologramBoundsSizeMeters = 1.0f;
        [SerializeField] private float damageHologramScanlineWidth = 0.11f;
        [SerializeField] private int damageHologramLayer;

        [Header("Physical Panel")]
        [SerializeField] private Transform dashboardPanelPlane;
        [SerializeField] private Vector2 panelHalfExtents = new Vector2(0.72f, 0.36f);
        [SerializeField] private int buttonColumns = 4;
        [SerializeField] private int buttonRows = 2;
        [SerializeField] private int buttonCount = 8;
        [SerializeField] private int externalFeedLeverButtonIndex = 7;
        [SerializeField] private float buttonPressedLocalZ = -0.035f;
        [SerializeField] private Transform[] buttonTransforms = Array.Empty<Transform>();

        [Header("Screen Render Targets")]
        [SerializeField] private Camera offscreenUiCamera;
        [SerializeField] private Camera exteriorFeedCamera;
        [SerializeField] private Renderer centralScreenRenderer;
        [SerializeField] private Texture staticExternalNoiseTexture;
        [SerializeField] private int uiRenderTextureWidth = 1024;
        [SerializeField] private int uiRenderTextureHeight = 512;
        [SerializeField] private int externalRenderTextureWidth = 768;
        [SerializeField] private int externalRenderTextureHeight = 432;

        [Header("Off-screen Text")]
        [SerializeField] private TMP_Text powerLabel;
        [SerializeField] private TMP_Text oxygenLabel;
        [SerializeField] private TMP_Text sonarLabel;
        [SerializeField] private TMP_Text statusLabel;

        [Header("Power Node")]
        [SerializeField] private int submarinePowerGridIndex;
        [SerializeField] private int submarineNodeVoltageIndex;

        private readonly char[] _powerTextBuffer = new char[TextBufferCapacity];
        private readonly char[] _oxygenTextBuffer = new char[TextBufferCapacity];
        private readonly char[] _sonarTextBuffer = new char[TextBufferCapacity];
        private readonly char[] _statusTextBuffer = new char[TextBufferCapacity];
        private readonly float[] _damageRoomWaterUpload = new float[MaxDamageHologramRooms]; // COLD ALLOC: float[32] - habitat room flood upload staging - owner: VehicleSubOsCockpitRuntime
        private readonly Vector4[] _damageFallbackPoint = new Vector4[FallbackDamageWarningPoints]; // COLD ALLOC: Vector4[7] - static warning glyph upload fallback - owner: VehicleSubOsCockpitRuntime
        private readonly GraphicsBuffer.IndirectDrawIndexedArgs[] _damageHologramArgsUpload = new GraphicsBuffer.IndirectDrawIndexedArgs[1]; // COLD ALLOC: IndirectDrawIndexedArgs[1] - cached damage hologram args upload - owner: VehicleSubOsCockpitRuntime

        private VaultGenerationHandle<byte> _buttonStatesHandle;
        private VaultGenerationHandle<byte> _buttonTargetsHandle;
        private VaultGenerationHandle<float> _buttonProgressHandle;
        private VaultGenerationHandle<float> _buttonOffsetsHandle;
        private VaultGenerationHandle<CockpitButtonBasePosition> _buttonBaseLocalPositionsHandle;
        private VaultGenerationHandle<float4x4> _buttonMatricesHandle;
        private VaultGenerationHandle<CockpitTelemetryEntry> _telemetryRingHandle;

        private GraphicsBuffer _sonarTapBufferA;
        private GraphicsBuffer _sonarTapBufferB;
        private GraphicsBuffer _activeSonarTapBuffer;
        private GraphicsBuffer _radarBlipBuffer;
        private GraphicsBuffer _radarArgsBufferA;
        private GraphicsBuffer _radarArgsBufferB;
        private GraphicsBuffer _activeRadarArgsBuffer;
        private GraphicsBuffer _buttonMatrixBufferA;
        private GraphicsBuffer _buttonMatrixBufferB;
        private GraphicsBuffer _activeButtonMatrixBuffer;
        private GraphicsBuffer _damageProxyVertexBufferA;
        private GraphicsBuffer _damageProxyVertexBufferB;
        private GraphicsBuffer _activeDamageProxyVertexBuffer;
        private GraphicsBuffer _damagePointBuffer;
        private GraphicsBuffer _damageArgsBuffer;
        private GraphicsBuffer _damageRoomWaterBufferA;
        private GraphicsBuffer _damageRoomWaterBufferB;
        private GraphicsBuffer _activeDamageRoomWaterBuffer;
        private Material _radarRuntimeMaterial;
        private Material _damageRuntimeMaterial;
        private Mesh _runtimeRadarQuad;
        private Mesh _runtimeDamageCube;
        private readonly MaterialPropertyBlock _screenPropertyBlock = new MaterialPropertyBlock(); // COLD ALLOC: MaterialPropertyBlock[1] - cockpit screen per-renderer properties - owner: VehicleSubOsCockpitRuntime
        private int _sonarTapUploadBufferIndex;
        private int _radarArgsUploadBufferIndex;
        private RenderTexture _uiRenderTexture;
        private RenderTexture _externalRenderTexture;
        private IRenderTexturePoolService _externalRenderTexturePoolOwner;
        private IRenderTexturePoolService _cachedRenderTexturePool;
        private IPlayerCriticalSonarEchoReadModel _cachedPlayerCriticalAudio;
        private IGroundRadarService _cachedGroundRadar;
        private IHabitatGraphService _cachedHabitatGraph;
        private IPowerGridService _cachedPowerGrid;
        private IDataVault _dataVault;

        private JobHandle _buttonJobHandle;
        private bool _buttonJobScheduled;
        private bool _buttonJobBuffersLocked;
        private bool _registeredLateFrame;
        private bool _registeredRenderable;
        private bool _hotSwapListenerRegistered;
        private bool _externalFeedRequested;
        private bool _externalFeedActive;
        private bool _externalFeedStateDirty = true;
        private bool _radarPowered;
        private bool _screenDirty = true;
        private bool _offscreenUiCameraRenderRequested = true;
        private bool _buttonAnimationActive = true;
        private bool _buttonUploadDirty = true;
        private bool _buttonBasesInitialized;
        private bool _resourcesReady;
        private bool _resourceRefreshDirty;
        private bool _radarResourcesReady;
        private bool _damageHologramResourcesReady;
        private bool _radarMaterialBufferBound;
        private bool _damageHologramMaterialBufferBound;
        private bool _damageHologramFallbackPointUploaded;
        private bool _damageHologramFallbackWarningActive;
        private bool _damageHologramUsingFallbackGlyph;
        private bool _damageHologramHadSignal;
        private bool _radarUsingGpr;
        private RenderTextureFormat _uiRenderTextureFormat = RenderTextureFormat.ARGB32;
        private int _radarKernel = -1;
        private int _radarThreadGroupSizeX;
        private int _radarCapacity;
        private int _radarPointsPerTap = MinQualityRadarPointsPerTap;
        private int _radarActivePoints;
        private int _damageHologramKernel = -1;
        private int _damageHologramThreadGroupSizeX;
        private int _damageProxyVertexCount;
        private int _buttonMatrixUploadIndex;
        private int _damageProxyUploadIndex;
        private int _damageRoomWaterUploadIndex;
        private int _damageHologramEstimatedPoints;
        private int _damageKnownActiveDentCount;
        private int _damageLastHullSignalFrame = -1;
        private int _damageLastImpactSignalFrame = -1;
        private int _damageRoomCount;
        private int _damageRoomSequence = -1;
        private int _lastSonarSequence = -1;
        private int _lastGprSequence = -1;
        private int _lastRadarDispatchSequence = -1;
        private int _lastRadarDispatchVisualPointCount;
        private int _lastRadarArgsInstanceCount = -1;
        private int _telemetryCursor;
        private int _telemetryWriteIndex;
        private int _telemetryPublishFrame;
        private int _lastPowerDisplayPercent = InvalidDisplayBucket;
        private int _lastOxygenDisplayPercent = InvalidDisplayBucket;
        private int _lastSonarDisplayPoints = InvalidDisplayBucket;
        private int _lastSonarDisplayPowered = InvalidDisplayBucket;
        private int _lastStatusDisplayMode = InvalidDisplayBucket;
        private Mesh _lastRadarArgsMesh;
        private int _nanDumped;
        private int _cockpitInteractions;
        private float _latestPowerRatio = 1f;
        private float _nodeVoltageSupplyRatio = 1f;
        private float _lastScreenPower = -1f;
        private float _latestOxygenNormalized = 1f;
        private float _latestCarbonDioxideNormalized;
        private float _latestSpeedKnots;
        private float _damageFlicker;
        private float _damageHologramFlickerTimer;
        private float _damageHologramFlood01;
        private uint _damageHologramFlickerSeed;
        private float _lastRadarDispatchPower = -1f;
        private float _lastRadarDispatchFlicker = -1f;
        private float _lastExternalFeedBlend = -1f;
        private float _screenUpdateAccumulator;
        private Texture _lastScreenTexture;
        private GraphicsBuffer _lastRadarMaterialBlipBuffer;
        private GraphicsBuffer _lastRadarMaterialGprBuffer;
        private Mesh _lastDamageProxyMesh;
        private Mesh _lastDamageArgsMesh;
        private Vector4 _damageProxyBounds = new Vector4(-0.75f, 0.75f, -0.45f, 0.35f);
        private Vector3[] _damageProxyUploadVertices;
        private List<Vector3> _damageProxySourceVertices;
        private int _lastDamageArgsInstanceCount = int.MinValue;
        private float _qualityWeight01 = 1f;
        private bool _graphicsResourceDisposalPending;
        private bool _damageRoomWaterUploadPending;
        private float _cheapVisualWeight01;
        private float _externalFeedWeight01 = 1f;

        /// <summary>
        /// GPU matrix buffer for cockpit button presentation consumers.
        /// </summary>
        public GraphicsBuffer ButtonMatrixBuffer => _activeButtonMatrixBuffer;

        /// <summary>
        /// Number of currently drawable holographic radar points after quality-weight clamping.
        /// </summary>
        public int RadarActivePoints => _radarActivePoints;

        /// <summary>
        /// Total cockpit button interactions recorded by this runtime instance.
        /// </summary>
        public int CockpitInteractions => _cockpitInteractions;

        public int HoloDamagePoints => _damageHologramEstimatedPoints;

        public int HoloProxyVertexCount => _damageProxyVertexCount;

        public float HologramFlood01 => SaturateFinite(_damageHologramFlood01, 0f);

        public byte HologramFlags => (byte)(BuildDamageHologramTelemetryFlags() & 0xffu);

        private void Awake()
        {
            InvalidateOffscreenTextCache();
            ResolveColdAssetReferences();
            CacheRegistryServicesCold();
            RefreshQualityPolicy(allowGraphicsResourceMutation: true);
            EnsureNativeResources();
            EnsureGraphicsResources();
            EnsureRenderTargets();
        }

        private void OnEnable()
        {
            InvalidateOffscreenTextCache();
            ResolveColdAssetReferences();
            CacheRegistryServicesCold();
            RefreshQualityPolicy(allowGraphicsResourceMutation: true);
            EnsureNativeResources();
            EnsureGraphicsResources();
            EnsureRenderTargets();
            HectonSubmarineOsEvents.Register(this);
            PowerGridTelemetryEvents.Register(this);
            TryRegisterHotSwapListener();
            TryRegisterRuntime();
            ApplyScreenMaterial();
            ApplyOffscreenUiCameraState();
        }

        private void OnDisable()
        {
            CompleteButtonJobForTeardown();
            HectonSubmarineOsEvents.Unregister(this);
            PowerGridTelemetryEvents.Unregister(this);
            TryUnregisterHotSwapListener();
            UnregisterRuntime();
            ReleaseExternalRenderTexture();
            if (offscreenUiCamera != null)
                offscreenUiCamera.enabled = false;
            _offscreenUiCameraRenderRequested = true;
        }

        private void OnDestroy()
        {
            CompleteButtonJobForTeardown();
            TryUnregisterHotSwapListener();
            ReleaseExternalRenderTexture();
            DisposeGraphicsResources();
            DisposeNativeResources();
            ReleaseUiRenderTexture();
            if (_radarRuntimeMaterial != null)
            {
                Destroy(_radarRuntimeMaterial);
                _radarRuntimeMaterial = null;
            }

            if (_runtimeRadarQuad != null)
            {
                Destroy(_runtimeRadarQuad);
                _runtimeRadarQuad = null;
            }

            if (_damageRuntimeMaterial != null)
            {
                Destroy(_damageRuntimeMaterial);
                _damageRuntimeMaterial = null;
            }

            if (_runtimeDamageCube != null)
            {
                Destroy(_runtimeDamageCube);
                _runtimeDamageCube = null;
            }
        }

        private void AdvanceCockpitFrameState(float deltaTime)
        {
            float safeDeltaTime = math.isfinite(deltaTime) ? math.max(0f, deltaTime) : 0f;
            RefreshQualityPolicy(allowGraphicsResourceMutation: false);
            if (!_resourcesReady)
            {
                _resourceRefreshDirty = true;
                return;
            }

            _nodeVoltageSupplyRatio = ResolveNodeVoltageSupplyRatio();
            _radarPowered = _nodeVoltageSupplyRatio >= RadarPowerCutoff;
            _damageFlicker = math.isfinite(_damageFlicker) ? math.max(0f, _damageFlicker - safeDeltaTime * 0.8f) : 0f;
            _damageHologramFlickerTimer = math.isfinite(_damageHologramFlickerTimer)
                ? math.max(0f, _damageHologramFlickerTimer - safeDeltaTime)
                : 0f;

            ConsumeDamageHologramSignals();
            RefreshDamageHologramFloodState();
            _externalFeedStateDirty = true;
            ScheduleButtonJob(safeDeltaTime);
            RecordTelemetry();
        }

        public void LateFrameTick()
        {
            AdvanceCockpitFrameState(SystemDispatcher.CurrentFrameDeltaTime);

            if (_resourceRefreshDirty || !_resourcesReady)
            {
                _resourceRefreshDirty = false;
                EnsureNativeResources();
                EnsureRenderTargets();
            }

            if (_resourcesReady)
            {
                if (_graphicsResourceDisposalPending)
                {
                    _graphicsResourceDisposalPending = false;
                    DisposeGraphicsResources();
                }

                if (_externalFeedStateDirty)
                {
                    _externalFeedStateDirty = false;
                    UpdateExternalFeedState();
                }

                if (ShouldRetryRadarGraphicsResources())
                    EnsureGraphicsResources();
                FlushDamageRoomWaterUpload();
                EnsureRenderTargets();
                UploadSonarTapsAndDispatchRadar();
                if (UpdateOffscreenText(SystemDispatcher.CurrentFrameUnscaledDeltaTime))
                    RequestOffscreenUiRender();
                ApplyScreenMaterial();
                ApplyOffscreenUiCameraState();
            }

            if (_buttonJobScheduled && DispatcherJobSwap.TryFinalizeCompleted(ref _buttonJobHandle))
            {
                _buttonJobScheduled = false;
                ReleaseButtonJobBufferLocks();
                UploadButtonMatrices();
                ApplyButtonTransforms();
                _buttonAnimationActive = HasButtonTransitions();
                _buttonUploadDirty = false;
            }
        }

        public void Render(float deltaTime)
        {
            RenderDamageHologram();
            RenderRadarPointCloud();
        }

        /// <summary>
        /// Attempts to resolve and press one cockpit button from a world-space aim ray.
        /// </summary>
        public bool TryPressFromRay(Vector3 rayOrigin, Vector3 rayDirection)
        {
            if (!TryResolvePanelHit(rayOrigin, rayDirection, out int buttonIndex))
                return false;

            PressCockpitButton(buttonIndex);
            return true;
        }

        /// <summary>
        /// Converts a world-space aim ray into the fixed physical button grid without broadphase physics.
        /// </summary>
        public bool TryResolvePanelHit(Vector3 rayOrigin, Vector3 rayDirection, out int buttonIndex)
        {
            buttonIndex = -1;
            if (!IsFinite(rayOrigin) || !IsFinite(rayDirection))
                return false;

            Transform panel = dashboardPanelPlane != null ? dashboardPanelPlane : transform;
            Matrix4x4 worldToLocal = panel.worldToLocalMatrix;
            Vector3 localOrigin = worldToLocal.MultiplyPoint3x4(rayOrigin);
            Vector3 localDirection = worldToLocal.MultiplyVector(rayDirection);
            if (!IsFinite(localOrigin) || !IsFinite(localDirection))
                return false;

            if (math.abs(localDirection.z) < 0.0001f)
                return false;

            float hitT = -localOrigin.z * math.rcp(localDirection.z);
            if (!math.isfinite(hitT) || hitT < 0f)
                return false;

            Vector3 localHit = localOrigin + localDirection * hitT;
            if (!IsFinite(localHit))
                return false;

            float2 halfExtents = ResolvePanelHalfExtents();
            if (math.abs(localHit.x) > halfExtents.x || math.abs(localHit.y) > halfExtents.y)
                return false;

            int columns = ResolveButtonColumns();
            int rows = ResolveButtonRows();
            float invPanelWidth = math.rcp(math.max(0.0001f, halfExtents.x * 2f));
            float invPanelHeight = math.rcp(math.max(0.0001f, halfExtents.y * 2f));
            float normalizedX = math.saturate((localHit.x + halfExtents.x) * invPanelWidth);
            float normalizedY = math.saturate((halfExtents.y - localHit.y) * invPanelHeight);
            int column = math.min(columns - 1, (int)math.floor(normalizedX * columns));
            int row = math.min(rows - 1, (int)math.floor(normalizedY * rows));
            int index = row * columns + column;
            int safeButtonCount = ResolveButtonCount();
            if ((uint)index >= (uint)safeButtonCount)
                return false;

            buttonIndex = index;
            return true;
        }

        /// <summary>
        /// Raises the radar damage flicker scalar; decay is handled by the dispatcher tick.
        /// </summary>
        public void SetDamageFlicker(float intensity)
        {
            if (!math.isfinite(intensity))
                return;

            _damageFlicker = math.max(_damageFlicker, math.saturate(intensity));
        }

        /// <summary>
        /// Writes the current cockpit telemetry ring to the VEHICLE_SUB_OS binary dump.
        /// </summary>
        public void RequestBlackboxDump()
        {
            DumpBlackbox();
        }

        void ISubmarineOsEventListener.OnSubmarineOsEvent(in SubmarineOsEventPayload payload)
        {
            if (payload.EventType != (ushort)SubmarineOsEventType.SnapshotUpdated)
                return;

            _latestPowerRatio = SaturateFinite(payload.PowerNormalized, _latestPowerRatio);
            _latestOxygenNormalized = SaturateFinite(payload.OxygenNormalized, _latestOxygenNormalized);
            _latestCarbonDioxideNormalized = SaturateFinite(payload.CarbonDioxideNormalized, _latestCarbonDioxideNormalized);
            _latestSpeedKnots = math.isfinite(payload.SpeedKnots) ? math.max(0f, payload.SpeedKnots) : 0f;
            _screenDirty = true;
        }

        void IPowerGridTelemetryListener.OnPowerGridTelemetryUpdated(in PowerGridTelemetrySnapshot snapshot)
        {
            _latestPowerRatio = SaturateFinite(snapshot.SupplyRatio, _latestPowerRatio);
            _screenDirty = true;
        }

        private void TryRegisterRuntime()
        {
            if (!_registeredLateFrame)
                _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.UI);
            if (!_registeredRenderable)
                _registeredRenderable = GlobalRegistry.Renderables.TryRegister(this);
        }

        private void UnregisterRuntime()
        {
            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.UI);
                _registeredLateFrame = false;
            }

            if (_registeredRenderable)
            {
                GlobalRegistry.Renderables.Unregister(this);
                _registeredRenderable = false;
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

        private void ResolveColdAssetReferences()
        {
#if UNITY_EDITOR
            if (radarCompute == null)
                radarCompute = AssetDatabase.LoadAssetAtPath<ComputeShader>(ComputeAssetPath);
            if (damageHologramCompute == null)
                damageHologramCompute = AssetDatabase.LoadAssetAtPath<ComputeShader>(DamageHologramComputeAssetPath);
            if (damageHologramMaterial == null)
                damageHologramMaterial = AssetDatabase.LoadAssetAtPath<Material>(DamageHologramMaterialAssetPath);
#endif
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.RenderTexturePoolRuntime:
                    _cachedRenderTexturePool = currentService as IRenderTexturePoolService;
                    break;
                case GlobalRegistryServiceSlot.PlayerCriticalAudioRuntime:
                    _cachedPlayerCriticalAudio = currentService as IPlayerCriticalSonarEchoReadModel;
                    InvalidateRadarDispatchCache();
                    break;
                case GlobalRegistryServiceSlot.GroundRadarRuntime:
                    _cachedGroundRadar = currentService as IGroundRadarService;
                    InvalidateRadarDispatchCache();
                    InvalidateRadarMaterialBinding();
                    break;
                case GlobalRegistryServiceSlot.PowerGrid:
                    _cachedPowerGrid = currentService as IPowerGridService;
                    break;
                case GlobalRegistryServiceSlot.Logistics:
                    if (currentService is IHabitatGraphService || previousService is IHabitatGraphService)
                        _cachedHabitatGraph = currentService as IHabitatGraphService;
                    break;
            }
        }

        private void CacheRegistryServicesCold()
        {
            _cachedRenderTexturePool = GlobalRegistry.RenderTexturePoolService;
            _cachedPlayerCriticalAudio = GlobalRegistry.PlayerCriticalSonarEcho;
            _cachedGroundRadar = GlobalRegistry.GroundRadar;
            _cachedHabitatGraph = GlobalRegistry.HabitatGraph;
            _cachedPowerGrid = GlobalRegistry.PowerGrid;
        }

        private void RefreshQualityPolicy(bool allowGraphicsResourceMutation)
        {
            float quality = math.saturate(HomeostasisBrain.GlobalQualityWeight);
            int capacity = ResolveRadarCapacity(quality);
            int pointsPerTap = ResolveRadarPointsPerTap(quality);
            float cheapVisualWeight = ResolveCheapVisualWeight(quality);
            float externalFeedWeight = ResolveExternalFeedWeight(quality);
            RenderTextureFormat format = ResolveUiRenderTextureFormat(quality);
            if (math.abs(quality - _qualityWeight01) <= 0.0001f &&
                capacity == _radarCapacity &&
                pointsPerTap == _radarPointsPerTap &&
                math.abs(cheapVisualWeight - _cheapVisualWeight01) <= 0.0001f &&
                math.abs(externalFeedWeight - _externalFeedWeight01) <= 0.0001f &&
                format == _uiRenderTextureFormat)
            {
                return;
            }

            _qualityWeight01 = quality;
            _cheapVisualWeight01 = cheapVisualWeight;
            _externalFeedWeight01 = externalFeedWeight;
            _radarPointsPerTap = pointsPerTap;
            _uiRenderTextureFormat = format;
            _screenDirty = true;
            if (capacity != _radarCapacity)
            {
                _radarCapacity = capacity;
                _radarResourcesReady = false;
                if (allowGraphicsResourceMutation)
                    DisposeGraphicsResources();
                else
                    _graphicsResourceDisposalPending = true;
                InvalidateRadarDispatchCache();
                _buttonUploadDirty = true;
                _buttonAnimationActive = true;
            }
        }

        private static int ResolveRadarCapacity(float qualityWeight01)
        {
            float curve = SmoothQuality(qualityWeight01);
            float continuous = math.lerp(MinQualityRadarPoints, MaxRadarPoints, curve);
            int quantized = (int)math.round(continuous * RadarCapacityQuantumInv) << 7;
            return math.clamp(quantized, MinQualityRadarPoints, MaxRadarPoints);
        }

        private static int ResolveRadarPointsPerTap(float qualityWeight01)
        {
            float curve = SmoothQuality(qualityWeight01);
            float continuous = math.lerp(MinQualityRadarPointsPerTap, MaxRadarPointsPerTap, curve);
            int quantized = (int)math.round(continuous * RadarPointTapQuantumInv) << 4;
            return math.clamp(quantized, MinQualityRadarPointsPerTap, MaxRadarPointsPerTap);
        }

        private static float ResolveCheapVisualWeight(float qualityWeight01)
        {
            float normalized = math.saturate((CheapVisualQualityThreshold - qualityWeight01) * CheapVisualQualityRampInv);
            return normalized * normalized * (3f - 2f * normalized);
        }

        private static float ResolveExternalFeedWeight(float qualityWeight01)
        {
            float normalized = math.saturate((qualityWeight01 - ExternalFeedEnableThreshold) * 2.7777777f);
            return normalized * normalized * (3f - 2f * normalized);
        }

        private static float SmoothQuality(float qualityWeight01)
        {
            float quality = math.saturate(qualityWeight01);
            return quality * quality * (3f - 2f * quality);
        }

        private void EnsureNativeResources()
        {
            int safeButtonCount = ResolveButtonCount();
            IDataVault vault = CacheDataVaultCold();
            if (vault == null)
            {
                _resourcesReady = false;
                return;
            }

            bool recreated = false;
            if (!EnsureCockpitVaultBuffer(ref _buttonStatesHandle, ButtonStatesBufferId, MaxButtons, NativeArrayOptions.ClearMemory, out bool statesRecreated) ||
                !EnsureCockpitVaultBuffer(ref _buttonTargetsHandle, ButtonTargetsBufferId, MaxButtons, NativeArrayOptions.ClearMemory, out bool targetsRecreated) ||
                !EnsureCockpitVaultBuffer(ref _buttonProgressHandle, ButtonProgressBufferId, MaxButtons, NativeArrayOptions.ClearMemory, out bool progressRecreated) ||
                !EnsureCockpitVaultBuffer(ref _buttonOffsetsHandle, ButtonOffsetsBufferId, MaxButtons, NativeArrayOptions.ClearMemory, out bool offsetsRecreated) ||
                !EnsureCockpitVaultBuffer(ref _buttonBaseLocalPositionsHandle, ButtonBaseLocalPositionsBufferId, MaxButtons, NativeArrayOptions.ClearMemory, out bool baseRecreated) ||
                !EnsureCockpitVaultBuffer(ref _buttonMatricesHandle, ButtonMatricesBufferId, MaxButtons, NativeArrayOptions.ClearMemory, out bool matricesRecreated) ||
                !EnsureCockpitVaultBuffer(ref _telemetryRingHandle, TelemetryRingBufferId, TelemetryCapacity, NativeArrayOptions.ClearMemory, out bool telemetryRecreated))
            {
                _resourcesReady = false;
                return;
            }

            recreated = statesRecreated || targetsRecreated || progressRecreated || offsetsRecreated ||
                        baseRecreated || matricesRecreated || telemetryRecreated;

            if (recreated)
                _buttonBasesInitialized = false;

            if ((recreated || !_buttonBasesInitialized) &&
                TryAcquireButtonBaseWriteBuffers(out NativeArray<CockpitButtonBasePosition> baseLocalPositions, out NativeArray<float4x4> matrices))
            {
                try
                {
                    for (int i = 0; i < safeButtonCount; i++)
                    {
                        Transform button = buttonTransforms != null && i < buttonTransforms.Length ? buttonTransforms[i] : null;
                        float3 fallbackPosition = ResolveButtonGridLocalPosition(i);
                        Vector3 baseVector = button != null
                            ? button.localPosition
                            : new Vector3(fallbackPosition.x, fallbackPosition.y, fallbackPosition.z);
                        float3 basePosition = IsFinite(baseVector) ? new float3(baseVector.x, baseVector.y, baseVector.z) : fallbackPosition;
                        baseLocalPositions[i] = new CockpitButtonBasePosition { LocalPosition = basePosition };
                        matrices[i] = float4x4.TRS(basePosition, quaternion.identity, new float3(1f));
                    }

                    _buttonBasesInitialized = true;
                    _buttonUploadDirty = true;
                    _buttonAnimationActive = true;
                }
                finally
                {
                    ReleaseButtonBaseWriteBuffers();
                }
            }
            else if (recreated || !_buttonBasesInitialized)
            {
                _resourcesReady = false;
                return;
            }

            _resourcesReady = HasButtonNativeResources(safeButtonCount);
        }

        private void DisposeNativeResources()
        {
            CompleteButtonJobForTeardown();
            ReleaseButtonJobBufferLocks();
            ReleaseCockpitVaultHandle(ref _buttonStatesHandle);
            ReleaseCockpitVaultHandle(ref _buttonTargetsHandle);
            ReleaseCockpitVaultHandle(ref _buttonProgressHandle);
            ReleaseCockpitVaultHandle(ref _buttonOffsetsHandle);
            ReleaseCockpitVaultHandle(ref _buttonBaseLocalPositionsHandle);
            ReleaseCockpitVaultHandle(ref _buttonMatricesHandle);
            ReleaseCockpitVaultHandle(ref _telemetryRingHandle);
            _buttonBasesInitialized = false;
            _resourcesReady = false;
        }

        private IDataVault CacheDataVaultCold()
        {
            if (_dataVault == null)
                _dataVault = GlobalRegistry.DataVault;

            return _dataVault;
        }

        private bool EnsureCockpitVaultBuffer<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            out bool recreated) where T : unmanaged
        {
            recreated = false;
            IDataVault vault = _dataVault;
            if (vault == null || requiredLength <= 0)
                return false;

            if (IsExactVaultHandle(in handle, bufferId) &&
                vault.TryReadOnlyHandle(in handle, out NativeArray<T>.ReadOnly existing) &&
                existing.IsCreated &&
                existing.Length >= requiredLength)
            {
                return true;
            }

            if (handle.BufferID != 0u && handle.Generation != 0u)
                vault.ReleaseBuffer(in handle);

            handle = vault.EnsureGenerationHandle<T>(
                bufferId,
                requiredLength,
                VaultOwnerSystemId,
                options);
            recreated = true;

            return IsExactVaultHandle(in handle, bufferId) &&
                   vault.TryReadOnlyHandle(in handle, out NativeArray<T>.ReadOnly resolved) &&
                   resolved.IsCreated &&
                   resolved.Length >= requiredLength;
        }

        private bool HasButtonNativeResources(int requiredButtonCount)
        {
            return TryReadButtonStates(requiredButtonCount, out _) &&
                   TryReadButtonTargets(requiredButtonCount, out _) &&
                   TryReadButtonProgress(requiredButtonCount, out _) &&
                   TryReadButtonOffsets(requiredButtonCount, out _) &&
                   TryReadButtonBaseLocalPositions(requiredButtonCount, out _) &&
                   TryReadButtonMatrices(requiredButtonCount, out _) &&
                   TryReadTelemetryRing(out _);
        }

        private bool TryAcquireButtonBaseWriteBuffers(
            out NativeArray<CockpitButtonBasePosition> baseLocalPositions,
            out NativeArray<float4x4> matrices)
        {
            baseLocalPositions = default;
            matrices = default;
            IDataVault vault = _dataVault;
            if (vault == null ||
                !IsExactVaultHandle(in _buttonBaseLocalPositionsHandle, ButtonBaseLocalPositionsBufferId) ||
                !vault.TryAcquireWriteLock(in _buttonBaseLocalPositionsHandle, VaultOwnerSystemId, out baseLocalPositions))
            {
                return false;
            }

            if (!IsExactVaultHandle(in _buttonMatricesHandle, ButtonMatricesBufferId) ||
                !vault.TryAcquireWriteLock(in _buttonMatricesHandle, VaultOwnerSystemId, out matrices))
            {
                vault.ReleaseWriteLock(in _buttonBaseLocalPositionsHandle, VaultOwnerSystemId);
                baseLocalPositions = default;
                return false;
            }

            if (baseLocalPositions.IsCreated && baseLocalPositions.Length >= MaxButtons &&
                matrices.IsCreated && matrices.Length >= MaxButtons)
            {
                return true;
            }

            vault.ReleaseWriteLock(in _buttonMatricesHandle, VaultOwnerSystemId);
            vault.ReleaseWriteLock(in _buttonBaseLocalPositionsHandle, VaultOwnerSystemId);
            baseLocalPositions = default;
            matrices = default;
            return false;
        }

        private void ReleaseButtonBaseWriteBuffers()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return;

            if (IsExactVaultHandle(in _buttonMatricesHandle, ButtonMatricesBufferId))
                vault.ReleaseWriteLock(in _buttonMatricesHandle, VaultOwnerSystemId);

            if (IsExactVaultHandle(in _buttonBaseLocalPositionsHandle, ButtonBaseLocalPositionsBufferId))
                vault.ReleaseWriteLock(in _buttonBaseLocalPositionsHandle, VaultOwnerSystemId);
        }

        private bool TryAcquireButtonStateWriteBuffers(
            int requiredButtonCount,
            out NativeArray<byte> states,
            out NativeArray<byte> targets)
        {
            states = default;
            targets = default;
            IDataVault vault = _dataVault;
            if (vault == null ||
                _buttonJobBuffersLocked ||
                !IsExactVaultHandle(in _buttonStatesHandle, ButtonStatesBufferId) ||
                !vault.TryAcquireWriteLock(in _buttonStatesHandle, VaultOwnerSystemId, out states))
            {
                return false;
            }

            if (!IsExactVaultHandle(in _buttonTargetsHandle, ButtonTargetsBufferId) ||
                !vault.TryAcquireWriteLock(in _buttonTargetsHandle, VaultOwnerSystemId, out targets))
            {
                vault.ReleaseWriteLock(in _buttonStatesHandle, VaultOwnerSystemId);
                states = default;
                return false;
            }

            if (states.IsCreated && states.Length >= requiredButtonCount &&
                targets.IsCreated && targets.Length >= requiredButtonCount)
            {
                return true;
            }

            vault.ReleaseWriteLock(in _buttonTargetsHandle, VaultOwnerSystemId);
            vault.ReleaseWriteLock(in _buttonStatesHandle, VaultOwnerSystemId);
            states = default;
            targets = default;
            return false;
        }

        private void ReleaseButtonStateWriteBuffers()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return;

            if (IsExactVaultHandle(in _buttonTargetsHandle, ButtonTargetsBufferId))
                vault.ReleaseWriteLock(in _buttonTargetsHandle, VaultOwnerSystemId);

            if (IsExactVaultHandle(in _buttonStatesHandle, ButtonStatesBufferId))
                vault.ReleaseWriteLock(in _buttonStatesHandle, VaultOwnerSystemId);
        }

        private bool TryAcquireButtonJobBuffers(
            int requiredButtonCount,
            out NativeArray<byte> states,
            out NativeArray<byte> targets,
            out NativeArray<float> progress,
            out NativeArray<float> offsets,
            out NativeArray<CockpitButtonBasePosition> baseLocalPositions,
            out NativeArray<float4x4> matrices)
        {
            states = default;
            targets = default;
            progress = default;
            offsets = default;
            baseLocalPositions = default;
            matrices = default;
            IDataVault vault = _dataVault;
            if (vault == null || _buttonJobBuffersLocked)
                return false;

            bool statesLocked = false;
            bool targetsLocked = false;
            bool progressLocked = false;
            bool offsetsLocked = false;
            bool baseLocked = false;
            bool matricesLocked = false;
            bool success = false;

            try
            {
                if (!IsExactVaultHandle(in _buttonStatesHandle, ButtonStatesBufferId) ||
                    !vault.TryAcquireWriteLock(in _buttonStatesHandle, VaultOwnerSystemId, out states))
                    return false;
                statesLocked = true;

                if (!IsExactVaultHandle(in _buttonTargetsHandle, ButtonTargetsBufferId) ||
                    !vault.TryAcquireWriteLock(in _buttonTargetsHandle, VaultOwnerSystemId, out targets))
                    return false;
                targetsLocked = true;

                if (!IsExactVaultHandle(in _buttonProgressHandle, ButtonProgressBufferId) ||
                    !vault.TryAcquireWriteLock(in _buttonProgressHandle, VaultOwnerSystemId, out progress))
                    return false;
                progressLocked = true;

                if (!IsExactVaultHandle(in _buttonOffsetsHandle, ButtonOffsetsBufferId) ||
                    !vault.TryAcquireWriteLock(in _buttonOffsetsHandle, VaultOwnerSystemId, out offsets))
                    return false;
                offsetsLocked = true;

                if (!IsExactVaultHandle(in _buttonBaseLocalPositionsHandle, ButtonBaseLocalPositionsBufferId) ||
                    !vault.TryAcquireWriteLock(in _buttonBaseLocalPositionsHandle, VaultOwnerSystemId, out baseLocalPositions))
                    return false;
                baseLocked = true;

                if (!IsExactVaultHandle(in _buttonMatricesHandle, ButtonMatricesBufferId) ||
                    !vault.TryAcquireWriteLock(in _buttonMatricesHandle, VaultOwnerSystemId, out matrices))
                    return false;
                matricesLocked = true;

                if (!states.IsCreated || states.Length < requiredButtonCount ||
                    !targets.IsCreated || targets.Length < requiredButtonCount ||
                    !progress.IsCreated || progress.Length < requiredButtonCount ||
                    !offsets.IsCreated || offsets.Length < requiredButtonCount ||
                    !baseLocalPositions.IsCreated || baseLocalPositions.Length < requiredButtonCount ||
                    !matrices.IsCreated || matrices.Length < requiredButtonCount)
                {
                    return false;
                }

                _buttonJobBuffersLocked = true;
                success = true;
                return true;
            }
            finally
            {
                if (!success)
                {
                    if (matricesLocked)
                        vault.ReleaseWriteLock(in _buttonMatricesHandle, VaultOwnerSystemId);
                    if (baseLocked)
                        vault.ReleaseWriteLock(in _buttonBaseLocalPositionsHandle, VaultOwnerSystemId);
                    if (offsetsLocked)
                        vault.ReleaseWriteLock(in _buttonOffsetsHandle, VaultOwnerSystemId);
                    if (progressLocked)
                        vault.ReleaseWriteLock(in _buttonProgressHandle, VaultOwnerSystemId);
                    if (targetsLocked)
                        vault.ReleaseWriteLock(in _buttonTargetsHandle, VaultOwnerSystemId);
                    if (statesLocked)
                        vault.ReleaseWriteLock(in _buttonStatesHandle, VaultOwnerSystemId);
                }
            }
        }

        private void ReleaseButtonJobBufferLocks()
        {
            if (!_buttonJobBuffersLocked)
                return;

            IDataVault vault = _dataVault;
            if (vault != null)
            {
                if (IsExactVaultHandle(in _buttonMatricesHandle, ButtonMatricesBufferId))
                    vault.ReleaseWriteLock(in _buttonMatricesHandle, VaultOwnerSystemId);
                if (IsExactVaultHandle(in _buttonBaseLocalPositionsHandle, ButtonBaseLocalPositionsBufferId))
                    vault.ReleaseWriteLock(in _buttonBaseLocalPositionsHandle, VaultOwnerSystemId);
                if (IsExactVaultHandle(in _buttonOffsetsHandle, ButtonOffsetsBufferId))
                    vault.ReleaseWriteLock(in _buttonOffsetsHandle, VaultOwnerSystemId);
                if (IsExactVaultHandle(in _buttonProgressHandle, ButtonProgressBufferId))
                    vault.ReleaseWriteLock(in _buttonProgressHandle, VaultOwnerSystemId);
                if (IsExactVaultHandle(in _buttonTargetsHandle, ButtonTargetsBufferId))
                    vault.ReleaseWriteLock(in _buttonTargetsHandle, VaultOwnerSystemId);
                if (IsExactVaultHandle(in _buttonStatesHandle, ButtonStatesBufferId))
                    vault.ReleaseWriteLock(in _buttonStatesHandle, VaultOwnerSystemId);
            }

            _buttonJobBuffersLocked = false;
        }

        private bool TryAcquireTelemetryWriteBuffer(out NativeArray<CockpitTelemetryEntry> telemetryRing)
        {
            telemetryRing = default;
            IDataVault vault = _dataVault;
            if (vault == null ||
                !IsExactVaultHandle(in _telemetryRingHandle, TelemetryRingBufferId) ||
                !vault.TryAcquireWriteLock(in _telemetryRingHandle, VaultOwnerSystemId, out telemetryRing))
            {
                return false;
            }

            if (telemetryRing.IsCreated && telemetryRing.Length >= TelemetryCapacity)
                return true;

            vault.ReleaseWriteLock(in _telemetryRingHandle, VaultOwnerSystemId);
            telemetryRing = default;
            return false;
        }

        private void ReleaseTelemetryWriteBuffer()
        {
            IDataVault vault = _dataVault;
            if (vault != null && IsExactVaultHandle(in _telemetryRingHandle, TelemetryRingBufferId))
                vault.ReleaseWriteLock(in _telemetryRingHandle, VaultOwnerSystemId);
        }

        private bool TryReadButtonStates(int requiredButtonCount, out NativeArray<byte>.ReadOnly states)
        {
            return TryReadCockpitVaultBuffer(in _buttonStatesHandle, ButtonStatesBufferId, requiredButtonCount, out states);
        }

        private bool TryReadButtonTargets(int requiredButtonCount, out NativeArray<byte>.ReadOnly targets)
        {
            return TryReadCockpitVaultBuffer(in _buttonTargetsHandle, ButtonTargetsBufferId, requiredButtonCount, out targets);
        }

        private bool TryReadButtonProgress(int requiredButtonCount, out NativeArray<float>.ReadOnly progress)
        {
            return TryReadCockpitVaultBuffer(in _buttonProgressHandle, ButtonProgressBufferId, requiredButtonCount, out progress);
        }

        private bool TryReadButtonOffsets(int requiredButtonCount, out NativeArray<float>.ReadOnly offsets)
        {
            return TryReadCockpitVaultBuffer(in _buttonOffsetsHandle, ButtonOffsetsBufferId, requiredButtonCount, out offsets);
        }

        private bool TryReadButtonBaseLocalPositions(int requiredButtonCount, out NativeArray<CockpitButtonBasePosition>.ReadOnly baseLocalPositions)
        {
            return TryReadCockpitVaultBuffer(in _buttonBaseLocalPositionsHandle, ButtonBaseLocalPositionsBufferId, requiredButtonCount, out baseLocalPositions);
        }

        private bool TryReadButtonMatrices(int requiredButtonCount, out NativeArray<float4x4> matrices)
        {
            return TryReadMutableCockpitVaultBuffer(in _buttonMatricesHandle, ButtonMatricesBufferId, requiredButtonCount, out matrices);
        }

        private bool TryReadTelemetryRing(out NativeArray<CockpitTelemetryEntry>.ReadOnly telemetryRing)
        {
            return TryReadCockpitVaultBuffer(in _telemetryRingHandle, TelemetryRingBufferId, TelemetryCapacity, out telemetryRing);
        }

        private bool TryReadCockpitVaultBuffer<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T>.ReadOnly buffer) where T : unmanaged
        {
            buffer = default;
            IDataVault vault = _dataVault;
            return vault != null &&
                   requiredLength > 0 &&
                   IsExactVaultHandle(in handle, bufferId) &&
                   vault.TryReadOnlyHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private bool TryReadMutableCockpitVaultBuffer<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : unmanaged
        {
            buffer = default;
            IDataVault vault = _dataVault;
            return vault != null &&
                   requiredLength > 0 &&
                   IsExactVaultHandle(in handle, bufferId) &&
                   vault.TryReadHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private void ReleaseCockpitVaultHandle<T>(ref VaultGenerationHandle<T> handle) where T : unmanaged
        {
            IDataVault vault = _dataVault;
            if (vault != null && handle.BufferID != 0u && handle.Generation != 0u)
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private static bool IsExactVaultHandle<T>(in VaultGenerationHandle<T> handle, BufferID expectedBufferId) where T : unmanaged
        {
            return handle.BufferID == unchecked((uint)(int)expectedBufferId) &&
                   handle.SystemID == (uint)VaultOwnerSystemId &&
                   handle.Generation != 0u;
        }

        private void EnsureGraphicsResources()
        {
            _radarCapacity = math.clamp(_radarCapacity <= 0 ? ResolveRadarCapacity(_qualityWeight01) : _radarCapacity, MinQualityRadarPoints, MaxRadarPoints);
            if (_buttonMatrixBufferA == null || _buttonMatrixBufferB == null)
            {
                ReleaseBuffer(ref _buttonMatrixBufferA);
                ReleaseBuffer(ref _buttonMatrixBufferB);
                _buttonMatrixBufferA = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float4x4>(MaxButtons); // COLD ALLOC: GraphicsBuffer[32] - kinematic dashboard matrix bridge A - owner: VehicleSubOsCockpitRuntime
                _buttonMatrixBufferB = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float4x4>(MaxButtons); // COLD ALLOC: GraphicsBuffer[32] - kinematic dashboard matrix bridge B - owner: VehicleSubOsCockpitRuntime
                _activeButtonMatrixBuffer = _buttonMatrixBufferA;
                _buttonMatrixUploadIndex = 0;
            }
            EnsureDamageHologramGraphicsResources();
            if (radarCompute == null || !SystemInfo.supportsComputeShaders)
            {
                _radarResourcesReady = false;
                return;
            }

            if (_sonarTapBufferA == null)
                _sonarTapBufferA = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<SonarEchoTap>(_radarCapacity); // COLD ALLOC: GraphicsBuffer[radarCapacity] - sonar tap upload bridge A - owner: VehicleSubOsCockpitRuntime
            if (_sonarTapBufferB == null)
                _sonarTapBufferB = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<SonarEchoTap>(_radarCapacity); // COLD ALLOC: GraphicsBuffer[radarCapacity] - sonar tap upload bridge B - owner: VehicleSubOsCockpitRuntime
            if (_activeSonarTapBuffer == null)
                _activeSonarTapBuffer = _sonarTapBufferA;
            if (_radarBlipBuffer == null)
                _radarBlipBuffer = GraphicsBufferUploadUtility.CreateStructuredBuffer<RadarBlipGpuData>(_radarCapacity); // COLD ALLOC: GraphicsBuffer[radarCapacity] - compute-written radar blips - owner: VehicleSubOsCockpitRuntime
            bool radarArgsBufferCreated = false;
            if (_radarArgsBufferA == null)
            {
                _radarArgsBufferA = new GraphicsBuffer(
                    GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Raw,
                    GraphicsBuffer.UsageFlags.LockBufferForWrite,
                    1,
                    GraphicsBuffer.IndirectDrawIndexedArgs.size); // COLD ALLOC: GraphicsBuffer[1] - radar indirect draw args A - owner: VehicleSubOsCockpitRuntime
                radarArgsBufferCreated = true;
            }

            if (_radarArgsBufferB == null)
            {
                _radarArgsBufferB = new GraphicsBuffer(
                    GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Raw,
                    GraphicsBuffer.UsageFlags.LockBufferForWrite,
                    1,
                    GraphicsBuffer.IndirectDrawIndexedArgs.size); // COLD ALLOC: GraphicsBuffer[1] - radar indirect draw args B - owner: VehicleSubOsCockpitRuntime
                radarArgsBufferCreated = true;
            }

            if (_activeRadarArgsBuffer == null)
                _activeRadarArgsBuffer = _radarArgsBufferA;

            if (_radarRuntimeMaterial == null && radarBlipMaterial != null)
                _radarRuntimeMaterial = new Material(radarBlipMaterial); // COLD ALLOC: material instance prevents shared radar shader state bleed.
            if (radarBlipMesh == null && _runtimeRadarQuad == null)
                _runtimeRadarQuad = CreateRadarQuadMesh();
            if (radarArgsBufferCreated)
            {
                InvalidateRadarArgsCache();
                UpdateRadarArgs(0);
            }
            if (radarCompute != null && _radarKernel < 0)
            {
                _radarKernel = ResolveSupportedKernel(radarCompute, "KTranslateSonarTaps");
                _radarThreadGroupSizeX = ResolveKernelThreadGroupSizeX(
                    radarCompute,
                    _radarKernel);
            }

            _radarResourcesReady = _sonarTapBufferA != null &&
                                   _sonarTapBufferB != null &&
                                   _activeSonarTapBuffer != null &&
                                   _radarBlipBuffer != null &&
                                   _radarArgsBufferA != null &&
                                   _radarArgsBufferB != null &&
                                   _activeRadarArgsBuffer != null &&
                                   radarCompute != null &&
                                   _radarKernel >= 0 &&
                                   _radarThreadGroupSizeX > 0;
        }

        private void DisposeGraphicsResources()
        {
            ReleaseBuffer(ref _sonarTapBufferA);
            ReleaseBuffer(ref _sonarTapBufferB);
            _activeSonarTapBuffer = null;
            ReleaseBuffer(ref _radarBlipBuffer);
            ReleaseBuffer(ref _radarArgsBufferA);
            ReleaseBuffer(ref _radarArgsBufferB);
            _activeRadarArgsBuffer = null;
            ReleaseBuffer(ref _buttonMatrixBufferA);
            ReleaseBuffer(ref _buttonMatrixBufferB);
            _activeButtonMatrixBuffer = null;
            ReleaseBuffer(ref _damageProxyVertexBufferA);
            ReleaseBuffer(ref _damageProxyVertexBufferB);
            _activeDamageProxyVertexBuffer = null;
            ReleaseBuffer(ref _damagePointBuffer);
            ReleaseBuffer(ref _damageArgsBuffer);
            ReleaseBuffer(ref _damageRoomWaterBufferA);
            ReleaseBuffer(ref _damageRoomWaterBufferB);
            _activeDamageRoomWaterBuffer = null;
            _buttonMatrixUploadIndex = 0;
            _sonarTapUploadBufferIndex = 0;
            _radarArgsUploadBufferIndex = 0;
            _damageProxyUploadIndex = 0;
            _damageRoomWaterUploadIndex = 0;
            _radarKernel = -1;
            _damageHologramKernel = -1;
            _radarThreadGroupSizeX = 0;
            _damageHologramThreadGroupSizeX = 0;
            _radarResourcesReady = false;
            _damageHologramResourcesReady = false;
            InvalidateRadarDispatchCache();
            InvalidateRadarArgsCache();
            InvalidateRadarMaterialBinding();
            _damageHologramMaterialBufferBound = false;
            _damageHologramFallbackPointUploaded = false;
            _damageHologramFallbackWarningActive = false;
            _damageHologramUsingFallbackGlyph = false;
            _lastDamageProxyMesh = null;
            _lastDamageArgsMesh = null;
            _lastDamageArgsInstanceCount = int.MinValue;
        }

        private static void ReleaseBuffer(ref GraphicsBuffer buffer)
        {
            if (buffer == null)
                return;

            buffer.Release();
            buffer = null;
        }

        private GraphicsBuffer ResolveButtonMatrixWriteBuffer()
        {
            GraphicsBuffer preferred = (_buttonMatrixUploadIndex & 1) == 0
                ? _buttonMatrixBufferB
                : _buttonMatrixBufferA;
            if (preferred != null && preferred.IsValid())
                return preferred;

            return _buttonMatrixBufferA != null && _buttonMatrixBufferA.IsValid()
                ? _buttonMatrixBufferA
                : _buttonMatrixBufferB;
        }

        private GraphicsBuffer ResolveDamageProxyWriteBuffer()
        {
            GraphicsBuffer preferred = (_damageProxyUploadIndex & 1) == 0
                ? _damageProxyVertexBufferB
                : _damageProxyVertexBufferA;
            if (preferred != null && preferred.IsValid())
                return preferred;

            return _damageProxyVertexBufferA != null && _damageProxyVertexBufferA.IsValid()
                ? _damageProxyVertexBufferA
                : _damageProxyVertexBufferB;
        }

        private GraphicsBuffer ResolveDamageRoomWaterWriteBuffer()
        {
            GraphicsBuffer preferred = (_damageRoomWaterUploadIndex & 1) == 0
                ? _damageRoomWaterBufferB
                : _damageRoomWaterBufferA;
            if (preferred != null && preferred.IsValid())
                return preferred;

            return _damageRoomWaterBufferA != null && _damageRoomWaterBufferA.IsValid()
                ? _damageRoomWaterBufferA
                : _damageRoomWaterBufferB;
        }

        private void EnsureDamageHologramGraphicsResources()
        {
            if (_damagePointBuffer == null)
            {
                _damagePointBuffer = new GraphicsBuffer(
                    GraphicsBuffer.Target.Append,
                    MaxDamageHologramPoints,
                    16); // COLD ALLOC: GraphicsBuffer[512 float4] - GPU append hologram point cloud - owner: VehicleSubOsCockpitRuntime
                _damageHologramMaterialBufferBound = false;
                _damageHologramFallbackPointUploaded = false;
                _damageHologramFallbackWarningActive = false;
            }

            if (_damageArgsBuffer == null)
            {
                _damageArgsBuffer = new GraphicsBuffer(
                    GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Raw,
                    1,
                    GraphicsBuffer.IndirectDrawIndexedArgs.size); // COLD ALLOC: GraphicsBuffer[1] - damage hologram indirect args - owner: VehicleSubOsCockpitRuntime
                UpdateDamageHologramArgs(0, true);
            }

            if (_damageRoomWaterBufferA == null || _damageRoomWaterBufferB == null)
            {
                ReleaseBuffer(ref _damageRoomWaterBufferA);
                ReleaseBuffer(ref _damageRoomWaterBufferB);
                _damageRoomWaterBufferA = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float>(MaxDamageHologramRooms); // COLD ALLOC: GraphicsBuffer[32 float] - room flood levels for hologram tint A - owner: VehicleSubOsCockpitRuntime
                _damageRoomWaterBufferB = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float>(MaxDamageHologramRooms); // COLD ALLOC: GraphicsBuffer[32 float] - room flood levels for hologram tint B - owner: VehicleSubOsCockpitRuntime
                GraphicsBufferUploadUtility.UploadArray(_damageRoomWaterBufferA, _damageRoomWaterUpload, MaxDamageHologramRooms);
                GraphicsBufferUploadUtility.UploadArray(_damageRoomWaterBufferB, _damageRoomWaterUpload, MaxDamageHologramRooms);
                _activeDamageRoomWaterBuffer = _damageRoomWaterBufferA;
                _damageRoomWaterUploadIndex = 0;
            }

            if (_damageRuntimeMaterial == null && damageHologramMaterial != null)
                _damageRuntimeMaterial = new Material(damageHologramMaterial); // COLD ALLOC: material instance prevents cockpit hologram shader state bleed.

            if (damagePointMesh == null && _runtimeDamageCube == null)
                _runtimeDamageCube = CreateDamageCubeMesh();

            EnsureDamageProxyVertexBuffer();

            if (damageHologramCompute != null && _damageHologramKernel < 0)
            {
                _damageHologramKernel = ResolveSupportedKernel(damageHologramCompute, DamageHologramKernelName);
                _damageHologramThreadGroupSizeX = ResolveKernelThreadGroupSizeX(
                    damageHologramCompute,
                    _damageHologramKernel);
            }

            bool fallbackReady = _damagePointBuffer != null &&
                                 _damageArgsBuffer != null &&
                                 _damageRuntimeMaterial != null &&
                                 ResolveDamagePointMesh() != null;
            bool computeReady = _damagePointBuffer != null &&
                                _damageArgsBuffer != null &&
                                _damageRuntimeMaterial != null &&
                                _activeDamageProxyVertexBuffer != null &&
                                _activeDamageRoomWaterBuffer != null &&
                                damageHologramCompute != null &&
                                _damageHologramKernel >= 0 &&
                                damageHologramCompute.IsSupported(_damageHologramKernel) &&
                                _damageHologramThreadGroupSizeX > 0 &&
                                ResolveDamagePointMesh() != null &&
                                _damageProxyVertexCount >= MinDamageProxyVertices;
            _damageHologramResourcesReady = fallbackReady || computeReady;
        }

        private void EnsureDamageProxyVertexBuffer()
        {
            Mesh mesh = damageProxyMeshLod3;
            if (ReferenceEquals(mesh, _lastDamageProxyMesh) && _activeDamageProxyVertexBuffer != null)
                return;

            _lastDamageProxyMesh = mesh;
            int sourceCount = 0;
            if (mesh != null && mesh.vertexCount > 0)
            {
                if (_damageProxySourceVertices == null)
                    _damageProxySourceVertices = new List<Vector3>(MaxDamageHologramPoints); // COLD ALLOC: List<Vector3>[512] - reusable LOD3 proxy vertex source - owner: VehicleSubOsCockpitRuntime

                mesh.GetVertices(_damageProxySourceVertices);
                sourceCount = _damageProxySourceVertices.Count;
            }

            bool useFallbackVertices = sourceCount < MinDamageProxyVertices;
            if (sourceCount < MinDamageProxyVertices)
            {
                sourceCount = FallbackDamageProxyVertices.Length;
            }

            int safeCount = math.clamp(sourceCount, MinDamageProxyVertices, MaxDamageHologramPoints);
            if (_damageProxyUploadVertices == null || _damageProxyUploadVertices.Length != safeCount)
                _damageProxyUploadVertices = new Vector3[safeCount]; // COLD ALLOC: Vector3[safeCount] - stable proxy vertex upload copy capped at 512 - owner: VehicleSubOsCockpitRuntime

            float minX = float.MaxValue;
            float maxX = float.MinValue;
            float minY = float.MaxValue;
            float maxY = float.MinValue;
            for (int i = 0; i < safeCount; i++)
            {
                Vector3 vertex = useFallbackVertices ? FallbackDamageProxyVertices[i] : _damageProxySourceVertices[i];
                if (!IsFinite(vertex))
                    vertex = Vector3.zero;

                _damageProxyUploadVertices[i] = vertex;
                minX = math.min(minX, vertex.x);
                maxX = math.max(maxX, vertex.x);
                minY = math.min(minY, vertex.y);
                maxY = math.max(maxY, vertex.y);
            }

            if (!math.isfinite(minX) || !math.isfinite(maxX) || maxX - minX < 0.0001f)
            {
                minX = -0.75f;
                maxX = 0.75f;
            }

            if (!math.isfinite(minY) || !math.isfinite(maxY) || maxY - minY < 0.0001f)
            {
                minY = -0.45f;
                maxY = 0.35f;
            }

            _damageProxyBounds = new Vector4(minX, maxX, minY, maxY);
            if (_damageProxyVertexBufferA == null || _damageProxyVertexBufferA.count != safeCount ||
                _damageProxyVertexBufferB == null || _damageProxyVertexBufferB.count != safeCount)
            {
                ReleaseBuffer(ref _damageProxyVertexBufferA);
                ReleaseBuffer(ref _damageProxyVertexBufferB);
                _damageProxyVertexBufferA = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<Vector3>(safeCount); // COLD ALLOC: GraphicsBuffer[proxy vertices] - submarine local-space damage hologram proxy A - owner: VehicleSubOsCockpitRuntime
                _damageProxyVertexBufferB = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<Vector3>(safeCount); // COLD ALLOC: GraphicsBuffer[proxy vertices] - submarine local-space damage hologram proxy B - owner: VehicleSubOsCockpitRuntime
                _activeDamageProxyVertexBuffer = _damageProxyVertexBufferA;
                _damageProxyUploadIndex = 0;
            }

            GraphicsBuffer proxyWriteBuffer = ResolveDamageProxyWriteBuffer();
            if (proxyWriteBuffer != null)
            {
                GraphicsBufferUploadUtility.UploadArray(proxyWriteBuffer, _damageProxyUploadVertices, safeCount);
                _activeDamageProxyVertexBuffer = proxyWriteBuffer;
                _damageProxyUploadIndex ^= 1;
            }
            _damageProxyVertexCount = safeCount;
        }

        private bool ShouldRetryRadarGraphicsResources()
        {
            if (radarCompute == null)
                return false;

            return !_radarResourcesReady ||
                   (_radarRuntimeMaterial == null && radarBlipMaterial != null) ||
                   (radarBlipMesh == null && _runtimeRadarQuad == null);
        }

        private void EnsureRenderTargets()
        {
            int width = ResolveUiWidth();
            int height = ResolveUiHeight();
            RenderTextureFormat format = _uiRenderTextureFormat;
            if (_uiRenderTexture == null ||
                _uiRenderTexture.width != width ||
                _uiRenderTexture.height != height ||
                _uiRenderTexture.format != format)
            {
                ReleaseUiRenderTexture();
                _uiRenderTexture = CreateRenderTexture(width, height, format, "VSOS_UI_RT");
                _lastScreenTexture = null;
                _screenDirty = true;
                RequestOffscreenUiRender();
            }

            if (offscreenUiCamera != null)
            {
                if (!offscreenUiCamera.orthographic)
                    offscreenUiCamera.orthographic = true;
                if (math.abs(offscreenUiCamera.depth + 100f) > 0.0001f)
                    offscreenUiCamera.depth = -100f;
                if (offscreenUiCamera.allowHDR)
                    offscreenUiCamera.allowHDR = false;
                if (offscreenUiCamera.allowMSAA)
                    offscreenUiCamera.allowMSAA = false;
                if (!ReferenceEquals(offscreenUiCamera.targetTexture, _uiRenderTexture))
                {
                    offscreenUiCamera.targetTexture = _uiRenderTexture;
                    RequestOffscreenUiRender();
                }
            }
        }

        private int ResolveUiWidth()
        {
            int minQualityWidth = math.clamp(uiRenderTextureWidth, MinUiRenderTextureWidth, MinQualityUiRenderTextureMaxWidth);
            int highWidth = math.max(MinUiRenderTextureWidth, uiRenderTextureWidth);
            return ResolveQualityDimension(minQualityWidth, highWidth);
        }

        private int ResolveUiHeight()
        {
            int minQualityHeight = math.clamp(uiRenderTextureHeight, MinUiRenderTextureHeight, MinQualityUiRenderTextureMaxHeight);
            int highHeight = math.max(MinUiRenderTextureHeight, uiRenderTextureHeight);
            return ResolveQualityDimension(minQualityHeight, highHeight);
        }

        private int ResolveExternalWidth()
        {
            int minQualityWidth = math.max(MinExternalRenderTextureWidth, math.min(externalRenderTextureWidth, MinQualityUiRenderTextureMaxWidth));
            int highWidth = math.max(MinExternalRenderTextureWidth, externalRenderTextureWidth);
            return ResolveQualityDimension(minQualityWidth, highWidth);
        }

        private int ResolveExternalHeight()
        {
            int minQualityHeight = math.max(MinExternalRenderTextureHeight, math.min(externalRenderTextureHeight, MinQualityUiRenderTextureMaxHeight));
            int highHeight = math.max(MinExternalRenderTextureHeight, externalRenderTextureHeight);
            return ResolveQualityDimension(minQualityHeight, highHeight);
        }

        private int ResolveQualityDimension(int minQualityValue, int highValue)
        {
            float curve = SmoothQuality(_qualityWeight01);
            int resolved = (int)math.round(math.lerp(minQualityValue, highValue, curve));
            return math.max(16, (resolved + 1) & ~1);
        }

        private static RenderTextureFormat ResolveUiRenderTextureFormat(float qualityWeight01)
        {
            if (ResolveCheapVisualWeight(qualityWeight01) < 0.5f)
                return RenderTextureFormat.ARGB32;

            return SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RGB565)
                ? RenderTextureFormat.RGB565
                : RenderTextureFormat.ARGB32;
        }

        private static RenderTexture CreateRenderTexture(int width, int height, RenderTextureFormat format, string name)
        {
            RenderTexture rt = new RenderTexture(math.max(16, width), math.max(16, height), 16, format)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
                autoGenerateMips = false,
                antiAliasing = 1
            };
            rt.Create();
            return rt;
        }

        private void UpdateExternalFeedState()
        {
            if (_externalFeedWeight01 <= 0.0001f)
            {
                ReleaseExternalRenderTexture();
                _externalFeedActive = false;
                return;
            }

            if (_externalFeedRequested)
            {
                AcquireExternalRenderTexture();
                _externalFeedActive = _externalRenderTexture != null;
                return;
            }

            ReleaseExternalRenderTexture();
            _externalFeedActive = false;
        }

        private void AcquireExternalRenderTexture()
        {
            if (_externalRenderTexture == null)
            {
                int width = ResolveExternalWidth();
                int height = ResolveExternalHeight();
                IRenderTexturePoolService pool = _cachedRenderTexturePool;
                _externalRenderTexture = pool != null
                    ? pool.Rent(width, height, RenderTextureFormat.ARGB32, this, 16)
                    : CreateRenderTexture(width, height, RenderTextureFormat.ARGB32, "VSOS_EXTCAM_RT");
                _externalRenderTexturePoolOwner = pool;
                if (_externalRenderTexture != null)
                {
                    _externalRenderTexture.name = "VSOS_EXTCAM_RT";
                    _externalRenderTexture.filterMode = FilterMode.Bilinear;
                    _externalRenderTexture.wrapMode = TextureWrapMode.Clamp;
                    if (!_externalRenderTexture.IsCreated())
                        _externalRenderTexture.Create();
                }
            }

            if (exteriorFeedCamera != null)
            {
                if (!ReferenceEquals(exteriorFeedCamera.targetTexture, _externalRenderTexture))
                    exteriorFeedCamera.targetTexture = _externalRenderTexture;
                if (!exteriorFeedCamera.enabled)
                    exteriorFeedCamera.enabled = true;
            }
        }

        private void ReleaseExternalRenderTexture()
        {
            if (exteriorFeedCamera != null)
            {
                if (exteriorFeedCamera.enabled)
                    exteriorFeedCamera.enabled = false;
                if (ReferenceEquals(exteriorFeedCamera.targetTexture, _externalRenderTexture))
                    exteriorFeedCamera.targetTexture = null;
            }

            if (_externalRenderTexture == null)
                return;

            RenderTexture released = _externalRenderTexture;
            _externalRenderTexture = null;

            IRenderTexturePoolService pool = _externalRenderTexturePoolOwner;
            _externalRenderTexturePoolOwner = null;
            if (pool != null)
                pool.Return(released);
            else
                DestroyRenderTexture(ref released);
        }

        private void ReleaseUiRenderTexture()
        {
            if (offscreenUiCamera != null && ReferenceEquals(offscreenUiCamera.targetTexture, _uiRenderTexture))
                offscreenUiCamera.targetTexture = null;

            DestroyRenderTexture(ref _uiRenderTexture);
        }

        private static void DestroyRenderTexture(ref RenderTexture rt)
        {
            if (rt == null)
                return;

            rt.Release();
            Destroy(rt);
            rt = null;
        }

        private bool UpdateOffscreenText(float deltaTime)
        {
            _screenUpdateAccumulator = math.min(_screenUpdateAccumulator + math.max(0f, deltaTime), 0.1f);
            if (!_screenDirty && _screenUpdateAccumulator < 0.1f)
                return false;
            if (!IsOffscreenUiVisible())
                return false;

            _screenUpdateAccumulator = 0f;
            _screenDirty = false;
            bool wrote = false;

            int powerPercent = ResolvePercentDisplayBucket(_nodeVoltageSupplyRatio);
            if (powerPercent != _lastPowerDisplayPercent &&
                WriteMetricLine(powerLabel, _powerTextBuffer, "PWR ".AsSpan(), powerPercent, "%".AsSpan()))
            {
                _lastPowerDisplayPercent = powerPercent;
                wrote = true;
            }

            int oxygenPercent = ResolvePercentDisplayBucket(_latestOxygenNormalized);
            if (oxygenPercent != _lastOxygenDisplayPercent &&
                WriteMetricLine(oxygenLabel, _oxygenTextBuffer, "O2  ".AsSpan(), oxygenPercent, "%".AsSpan()))
            {
                _lastOxygenDisplayPercent = oxygenPercent;
                wrote = true;
            }

            int sonarPowered = _radarPowered ? 1 : 0;
            if ((_radarActivePoints != _lastSonarDisplayPoints || sonarPowered != _lastSonarDisplayPowered) &&
                WriteSonarLine(_radarActivePoints, _radarPowered))
            {
                _lastSonarDisplayPoints = _radarActivePoints;
                _lastSonarDisplayPowered = sonarPowered;
                wrote = true;
            }

            int statusMode = ResolveStatusDisplayMode();
            if (statusMode != _lastStatusDisplayMode && WriteStatusLine(statusMode))
            {
                _lastStatusDisplayMode = statusMode;
                wrote = true;
            }

            return wrote;
        }

        private void RequestOffscreenUiRender()
        {
            _offscreenUiCameraRenderRequested = true;
        }

        private void ApplyOffscreenUiCameraState()
        {
            if (offscreenUiCamera == null)
                return;

            bool shouldRender = _offscreenUiCameraRenderRequested && _uiRenderTexture != null && IsOffscreenUiVisible();
            if (offscreenUiCamera.enabled != shouldRender)
                offscreenUiCamera.enabled = shouldRender;
            if (shouldRender)
                _offscreenUiCameraRenderRequested = false;
        }

        private bool IsOffscreenUiVisible()
        {
            if (_externalFeedActive && _externalRenderTexture != null)
                return false;
            if (_externalFeedWeight01 <= 0.0001f && _externalFeedRequested && staticExternalNoiseTexture != null)
                return false;
            return true;
        }

        private int ResolveStatusDisplayMode()
        {
            if (_externalFeedWeight01 <= 0.0001f && _externalFeedRequested)
                return staticExternalNoiseTexture != null ? StatusModeExternalStatic : StatusModeExternalLocked;
            if (_externalFeedActive)
                return StatusModeExternalLive;
            return StatusModeInternalBus;
        }

        private static int ResolvePercentDisplayBucket(float normalized)
        {
            return math.clamp((int)math.round(SaturateFinite(normalized, 0f) * 100f), 0, 100);
        }

        private void InvalidateOffscreenTextCache()
        {
            _lastPowerDisplayPercent = InvalidDisplayBucket;
            _lastOxygenDisplayPercent = InvalidDisplayBucket;
            _lastSonarDisplayPoints = InvalidDisplayBucket;
            _lastSonarDisplayPowered = InvalidDisplayBucket;
            _lastStatusDisplayMode = InvalidDisplayBucket;
            _screenDirty = true;
        }

        private static bool WriteMetricLine(TMP_Text label, char[] buffer, ReadOnlySpan<char> prefix, int value, ReadOnlySpan<char> suffix)
        {
            if (label == null || buffer == null)
                return false;

            Span<char> span = buffer.AsSpan();
            int cursor = 0;
            ZeroGCFormatter.AppendToSpan(prefix, span, ref cursor);
            ZeroGCFormatter.AppendInt(math.max(0, value), span, ref cursor);
            ZeroGCFormatter.AppendToSpan(suffix, span, ref cursor);
            label.SetCharArray(buffer, 0, math.max(0, cursor));
            return true;
        }

        private bool WriteSonarLine(int activePoints, bool powered)
        {
            if (sonarLabel == null)
                return false;

            Span<char> span = _sonarTextBuffer.AsSpan();
            int cursor = 0;
            ZeroGCFormatter.AppendToSpan("SONAR ".AsSpan(), span, ref cursor);
            ZeroGCFormatter.AppendInt(math.max(0, activePoints), span, ref cursor);
            ZeroGCFormatter.AppendToSpan(powered ? " ACTIVE".AsSpan() : " DARK".AsSpan(), span, ref cursor);
            sonarLabel.SetCharArray(_sonarTextBuffer, 0, math.max(0, cursor));
            return true;
        }

        private bool WriteStatusLine(int statusMode)
        {
            if (statusLabel == null)
                return false;

            Span<char> span = _statusTextBuffer.AsSpan();
            int cursor = 0;
            switch (statusMode)
            {
                case StatusModeExternalLive:
                    ZeroGCFormatter.AppendToSpan("EXT CAM LIVE".AsSpan(), span, ref cursor);
                    break;
                case StatusModeExternalStatic:
                    ZeroGCFormatter.AppendToSpan("EXT STATIC".AsSpan(), span, ref cursor);
                    break;
                case StatusModeExternalLocked:
                    ZeroGCFormatter.AppendToSpan("EXT LOCKED".AsSpan(), span, ref cursor);
                    break;
                default:
                    ZeroGCFormatter.AppendToSpan("INTERNAL BUS".AsSpan(), span, ref cursor);
                    break;
            }

            statusLabel.SetCharArray(_statusTextBuffer, 0, math.max(0, cursor));
            return true;
        }

        private void UploadSonarTapsAndDispatchRadar()
        {
            _radarActivePoints = 0;
            if (!_radarResourcesReady ||
                !_radarPowered ||
                radarCompute == null ||
                _radarKernel < 0 ||
                !radarCompute.IsSupported(_radarKernel) ||
                !IsRadarDrawableReady())
            {
                ClearRadarDrawState();
                return;
            }

            if (TryUploadGroundRadarPingsAndDispatchRadar())
                return;

            IPlayerCriticalSonarEchoReadModel audioRuntime = _cachedPlayerCriticalAudio;
            if (audioRuntime == null ||
                !audioRuntime.TryGetCockpitSonarEchoTaps(out NativeArray<SonarEchoTap>.ReadOnly taps, out int tapCount, out int sequence))
            {
                ClearRadarDrawState();
                return;
            }

            int safeCount = math.clamp(tapCount, 0, math.min(_radarCapacity, taps.Length));
            if (safeCount <= 0)
            {
                ClearRadarDrawState();
                return;
            }

            int visualPointCount = ResolveRadarVisualPointCount(safeCount);
            if (visualPointCount <= 0)
            {
                ClearRadarDrawState();
                return;
            }

            int radarDispatchGroups = CeilDividePositive(visualPointCount, _radarThreadGroupSizeX);
            if (radarDispatchGroups <= 0)
            {
                ClearRadarDrawState();
                return;
            }

            if (sequence != _lastSonarSequence)
            {
                _lastSonarSequence = sequence;
                _screenDirty = true;
            }

            bool dispatchDirty = IsRadarDispatchDirty(sequence, visualPointCount);
            _radarActivePoints = visualPointCount;
            _radarUsingGpr = false;
            if (!dispatchDirty)
                return;

            GraphicsBuffer sonarWriteBuffer = (_sonarTapUploadBufferIndex & 1) == 0 ? _sonarTapBufferA : _sonarTapBufferB;
            if (sonarWriteBuffer == null || !sonarWriteBuffer.IsValid())
                return;

            NativeArray<SonarEchoTap> mapped = sonarWriteBuffer.LockBufferForWrite<SonarEchoTap>(0, safeCount);
            try
            {
                for (int i = 0; i < safeCount; i++)
                    mapped[i] = taps[i];
            }
            finally
            {
                sonarWriteBuffer.UnlockBufferAfterWrite<SonarEchoTap>(safeCount);
            }
            _activeSonarTapBuffer = sonarWriteBuffer;
            _sonarTapUploadBufferIndex ^= 1;

            radarCompute.SetBuffer(_radarKernel, SonarTapsId, _activeSonarTapBuffer);
            radarCompute.SetBuffer(_radarKernel, RadarBlipsId, _radarBlipBuffer);
            radarCompute.SetInt(InputTapCountId, safeCount);
            radarCompute.SetInt(OutputPointCountId, visualPointCount);
            radarCompute.SetInt(OutputCapacityId, _radarCapacity);
            radarCompute.SetInt(SequenceId, sequence);
            radarCompute.SetFloat(RadarRadiusMetersId, ResolveRadarRadiusMeters());
            radarCompute.SetFloat(MaxDelaySecondsId, DefaultMaxSonarDelaySeconds);
            radarCompute.SetFloat(PowerLevelId, _nodeVoltageSupplyRatio);
            radarCompute.SetFloat(DamageFlickerId, _damageFlicker);

            radarCompute.Dispatch(_radarKernel, radarDispatchGroups, 1, 1);

            CacheRadarDispatchState(sequence, visualPointCount);
            UpdateRadarArgs(visualPointCount);
        }

        private bool TryUploadGroundRadarPingsAndDispatchRadar()
        {
            IGroundRadarService groundRadar = _cachedGroundRadar;
            if (groundRadar == null ||
                !groundRadar.TryGetGprPingBuffer(out GraphicsBuffer buffer, out int activeCount, out int sequence) ||
                buffer == null)
            {
                return false;
            }

            int visualPointCount = math.clamp(activeCount, 0, _radarCapacity);
            if (visualPointCount <= 0)
                return false;

            _radarActivePoints = visualPointCount;
            _radarUsingGpr = true;
            if (sequence != _lastGprSequence)
            {
                _lastGprSequence = sequence;
                _screenDirty = true;
            }

            UpdateRadarArgs(visualPointCount);
            return true;
        }

        private bool IsRadarDispatchDirty(int sequence, int visualPointCount)
        {
            return sequence != _lastRadarDispatchSequence ||
                   visualPointCount != _lastRadarDispatchVisualPointCount ||
                   math.abs(_nodeVoltageSupplyRatio - _lastRadarDispatchPower) > RadarRedispatchPowerEpsilon ||
                   math.abs(_damageFlicker - _lastRadarDispatchFlicker) > RadarRedispatchFlickerEpsilon;
        }

        private void CacheRadarDispatchState(int sequence, int visualPointCount)
        {
            _lastRadarDispatchSequence = sequence;
            _lastRadarDispatchVisualPointCount = visualPointCount;
            _lastRadarDispatchPower = _nodeVoltageSupplyRatio;
            _lastRadarDispatchFlicker = _damageFlicker;
        }

        private void InvalidateRadarDispatchCache()
        {
            _lastRadarDispatchSequence = -1;
            _lastRadarDispatchVisualPointCount = 0;
            _lastRadarDispatchPower = -1f;
            _lastRadarDispatchFlicker = -1f;
        }

        private void ClearRadarDrawState()
        {
            _radarUsingGpr = false;
            UpdateRadarArgs(0);
            InvalidateRadarDispatchCache();
        }

        private void ConsumeDamageHologramSignals()
        {
            ReadOnlySpan<HullDeformedSignal> hullSignals = SignalBus<HullDeformedSignal>.GetFrameSnapshot();
            for (int i = 0; i < hullSignals.Length; i++)
            {
                HullDeformedSignal signal = hullSignals[i];
                if (!math.all(math.isfinite(signal.LocalPoint)) ||
                    !math.isfinite(signal.Radius) ||
                    !math.isfinite(signal.Depth))
                {
                    continue;
                }

                _damageLastHullSignalFrame = (int)(signal.Frame != 0u ? signal.Frame : Hecton8.Core.SystemDispatcher.CurrentFrameId);
                _damageKnownActiveDentCount = math.clamp(signal.ActiveDentCount, 0, 16);
                _damageHologramHadSignal = true;
                _screenDirty = true;
            }

            ReadOnlySpan<HighSpeedImpactSignal> impactSignals = SignalBus<HighSpeedImpactSignal>.GetFrameSnapshot();
            for (int i = 0; i < impactSignals.Length; i++)
            {
                HighSpeedImpactSignal signal = impactSignals[i];
                float impactSpeed = math.isfinite(signal.ImpactSpeed) ? math.max(0f, signal.ImpactSpeed) : 0f;
                float lostEnergy = math.isfinite(signal.LostKineticEnergy) ? math.max(0f, signal.LostKineticEnergy) : 0f;
                if (impactSpeed <= 0.01f && lostEnergy <= 0.01f)
                    continue;

                _damageLastImpactSignalFrame = (int)(signal.Frame != 0u ? signal.Frame : Hecton8.Core.SystemDispatcher.CurrentFrameId);
                _damageHologramFlickerTimer = DamageHologramFlickerSeconds;
                _damageHologramFlickerSeed = signal.SourceHash ^ (signal.TargetHash * 747796405u) ^ signal.Frame;
                SetDamageFlicker(math.saturate(impactSpeed * 0.025f + lostEnergy * 0.0002f));
                _screenDirty = true;
            }
        }

        private void RefreshDamageHologramFloodState()
        {
            IHabitatGraphService habitatGraph = _cachedHabitatGraph;
            if (habitatGraph == null || !habitatGraph.IsInitialized || habitatGraph.RoomCount <= 0)
            {
                if (_damageRoomCount != 0)
                    UploadDamageRoomWaterLevels(default, 0, 0u);
                return;
            }

            uint sequence = habitatGraph.FloodStateSequence;
            if (_damageRoomCount == habitatGraph.RoomCount && _damageRoomSequence == unchecked((int)sequence))
                return;

            NativeArray<float>.ReadOnly levels = habitatGraph.RoomWaterLevels;
            UploadDamageRoomWaterLevels(levels, habitatGraph.RoomCount, sequence);
        }

        private void UploadDamageRoomWaterLevels(NativeArray<float>.ReadOnly levels, int roomCount, uint sequence)
        {
            int safeCount = math.clamp(roomCount, 0, MaxDamageHologramRooms);
            _damageHologramFlood01 = 0f;
            for (int i = 0; i < MaxDamageHologramRooms; i++)
            {
                float level = 0f;
                if (i < safeCount && i < levels.Length)
                    level = SaturateFinite(levels[i], 0f);

                _damageRoomWaterUpload[i] = level;
                _damageHologramFlood01 = math.max(_damageHologramFlood01, level);
            }

            _damageRoomCount = safeCount;
            _damageRoomSequence = unchecked((int)sequence);
            _damageRoomWaterUploadPending = true;
        }

        private void FlushDamageRoomWaterUpload()
        {
            if (!_damageRoomWaterUploadPending)
                return;

            GraphicsBuffer roomWriteBuffer = ResolveDamageRoomWaterWriteBuffer();
            if (roomWriteBuffer == null)
                return;

            GraphicsBufferUploadUtility.UploadArray(roomWriteBuffer, _damageRoomWaterUpload, MaxDamageHologramRooms);
            _activeDamageRoomWaterBuffer = roomWriteBuffer;
            _damageRoomWaterUploadIndex ^= 1;
            _damageRoomWaterUploadPending = false;
        }

        private int ResolveRadarVisualPointCount(int tapCount)
        {
            int safeTapCount = math.max(0, tapCount);
            if (safeTapCount <= 0 || _radarCapacity <= 0)
                return 0;

            long requested = (long)safeTapCount * math.max(1, _radarPointsPerTap);
            return (int)math.min(_radarCapacity, requested);
        }

        private void UpdateRadarArgs(int instanceCount)
        {
            Mesh mesh = ResolveRadarMesh();
            if (_radarArgsBufferA == null || _radarArgsBufferB == null || mesh == null)
                return;

            int safeInstanceCount = math.max(0, instanceCount);
            if (safeInstanceCount == _lastRadarArgsInstanceCount && ReferenceEquals(mesh, _lastRadarArgsMesh))
                return;

            GraphicsBuffer argsWriteBuffer = (_radarArgsUploadBufferIndex & 1) == 0 ? _radarArgsBufferA : _radarArgsBufferB;
            if (argsWriteBuffer == null || !argsWriteBuffer.IsValid())
                return;

            NativeArray<GraphicsBuffer.IndirectDrawIndexedArgs> argsWrite =
                argsWriteBuffer.LockBufferForWrite<GraphicsBuffer.IndirectDrawIndexedArgs>(0, 1);
            try
            {
                GraphicsBuffer.IndirectDrawIndexedArgs drawArgs = default;
                drawArgs.indexCountPerInstance = mesh.GetIndexCount(0);
                drawArgs.instanceCount = (uint)safeInstanceCount;
                drawArgs.startIndex = mesh.GetIndexStart(0);
                drawArgs.baseVertexIndex = (uint)Mathf.Max(0, mesh.GetBaseVertex(0));
                drawArgs.startInstance = 0u;
                argsWrite[0] = drawArgs;
            }
            finally
            {
                argsWriteBuffer.UnlockBufferAfterWrite<GraphicsBuffer.IndirectDrawIndexedArgs>(1);
            }
            _activeRadarArgsBuffer = argsWriteBuffer;
            _radarArgsUploadBufferIndex ^= 1;
            _lastRadarArgsInstanceCount = safeInstanceCount;
            _lastRadarArgsMesh = mesh;
        }

        private void InvalidateRadarArgsCache()
        {
            _lastRadarArgsInstanceCount = -1;
            _lastRadarArgsMesh = null;
        }

        private void InvalidateRadarMaterialBinding()
        {
            _radarMaterialBufferBound = false;
            _lastRadarMaterialBlipBuffer = null;
            _lastRadarMaterialGprBuffer = null;
        }

        private void RenderRadarPointCloud()
        {
            if (_radarActivePoints <= 0 ||
                !_radarPowered ||
                !_radarResourcesReady ||
                _radarRuntimeMaterial == null ||
                _radarBlipBuffer == null ||
                _activeRadarArgsBuffer == null)
            {
                return;
            }

            Mesh mesh = ResolveRadarMesh();
            if (mesh == null)
                return;

            Transform anchor = radarDomeAnchor != null ? radarDomeAnchor : transform;
            Matrix4x4 radarLocalToWorld = anchor.localToWorldMatrix;
            if (!IsFinite(radarLocalToWorld))
                return;
            Vector4 anchorColumn = radarLocalToWorld.GetColumn(3);
            Vector3 anchorPosition = new Vector3(anchorColumn.x, anchorColumn.y, anchorColumn.z);

            IGroundRadarService groundRadar = null;
            GraphicsBuffer groundRadarBuffer = null;
            if (_radarUsingGpr && !TryResolveGroundRadarRenderBinding(out groundRadar, out groundRadarBuffer))
                return;

            if (_radarUsingGpr)
            {
                if (!_radarMaterialBufferBound || !ReferenceEquals(_lastRadarMaterialGprBuffer, groundRadarBuffer))
                {
                    _radarRuntimeMaterial.SetBuffer(HectonGroundRadarPingsId, groundRadarBuffer);
                    _radarRuntimeMaterial.SetFloat(HectonRadarProceduralId, 1f);
                    _radarRuntimeMaterial.SetFloat(HectonRadarGprProceduralId, 1f);
                    _lastRadarMaterialGprBuffer = groundRadarBuffer;
                    _radarMaterialBufferBound = true;
                }

                float3 origin = groundRadar.LastProbeOrigin;
                _radarRuntimeMaterial.SetVector(
                    HectonRadarGprOriginRadiusId,
                    new Vector4(origin.x, origin.y, origin.z, math.max(1f, groundRadar.ScanRadiusMeters)));
            }
            else if (!_radarMaterialBufferBound || !ReferenceEquals(_lastRadarMaterialBlipBuffer, _radarBlipBuffer))
            {
                _radarRuntimeMaterial.SetBuffer(HectonRadarBlipsId, _radarBlipBuffer);
                _radarRuntimeMaterial.SetFloat(HectonRadarProceduralId, 1f);
                _radarRuntimeMaterial.SetFloat(HectonRadarGprProceduralId, 0f);
                _lastRadarMaterialBlipBuffer = _radarBlipBuffer;
                _radarMaterialBufferBound = true;
            }
            else
            {
                _radarRuntimeMaterial.SetFloat(HectonRadarGprProceduralId, 0f);
            }

            _radarRuntimeMaterial.SetMatrix(HectonRadarLocalToWorldId, radarLocalToWorld);

            Bounds bounds = new Bounds(anchorPosition, Vector3.one * ResolveRadarBoundsSize());
            RenderParams renderParams = new RenderParams(_radarRuntimeMaterial)
            {
                worldBounds = bounds,
                layer = radarLayer,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                motionVectorMode = MotionVectorGenerationMode.ForceNoMotion
            };
            UnityEngine.Graphics.RenderMeshIndirect(renderParams, mesh, _activeRadarArgsBuffer, 1, 0);
        }

        private void RenderDamageHologram()
        {
            if (!_radarPowered)
            {
                _damageHologramEstimatedPoints = 0;
                return;
            }

            if (!_damageHologramResourcesReady)
            {
                EnsureDamageHologramGraphicsResources();
                if (!_damageHologramResourcesReady)
                    return;
            }

            Mesh mesh = ResolveDamagePointMesh();
            if (mesh == null || _damageRuntimeMaterial == null || _damagePointBuffer == null || _damageArgsBuffer == null)
                return;

            Transform anchor = damageHologramAnchor != null
                ? damageHologramAnchor
                : radarDomeAnchor != null
                    ? radarDomeAnchor
                    : transform;
            Matrix4x4 hologramLocalToWorld = anchor.localToWorldMatrix;
            if (!IsFinite(hologramLocalToWorld))
                return;

            if (CanDispatchDamageHologramCompute())
                DispatchDamageHologramCompute();
            else
                UploadFallbackDamageHologramGlyph();

            BindDamageHologramMaterial(hologramLocalToWorld);
            Vector4 anchorColumn = hologramLocalToWorld.GetColumn(3);
            Vector3 anchorPosition = new Vector3(anchorColumn.x, anchorColumn.y, anchorColumn.z);
            Bounds bounds = new Bounds(anchorPosition, Vector3.one * ResolveDamageHologramBoundsSize());
            UnityEngine.Graphics.DrawMeshInstancedIndirect(
                mesh,
                0,
                _damageRuntimeMaterial,
                bounds,
                _damageArgsBuffer,
                0,
                null,
                ShadowCastingMode.Off,
                false,
                damageHologramLayer,
                Camera.current);
        }

        private void DispatchDamageHologramCompute()
        {
            if (damageHologramCompute == null ||
                _damageHologramKernel < 0 ||
                !damageHologramCompute.IsSupported(_damageHologramKernel) ||
                _activeDamageProxyVertexBuffer == null ||
                _damageProxyVertexCount <= 0 ||
                _activeDamageRoomWaterBuffer == null)
            {
                UpdateDamageHologramArgs(0, true);
                _damageHologramEstimatedPoints = 0;
                return;
            }

            _damageHologramUsingFallbackGlyph = false;
            int pointBudget = ResolveDamageHologramPointBudget();
            int damageDispatchGroups = CeilDividePositive(_damageProxyVertexCount, _damageHologramThreadGroupSizeX);
            if (damageDispatchGroups <= 0)
            {
                UpdateDamageHologramArgs(0, true);
                _damageHologramEstimatedPoints = 0;
                return;
            }

            _damagePointBuffer.SetCounterValue(0u);
            damageHologramCompute.SetBuffer(_damageHologramKernel, DamageProxyVerticesId, _activeDamageProxyVertexBuffer);
            damageHologramCompute.SetBuffer(_damageHologramKernel, DamageHologramPointsId, _damagePointBuffer);
            damageHologramCompute.SetBuffer(_damageHologramKernel, DamageRoomWaterLevelsId, _activeDamageRoomWaterBuffer);
            Vector4 computeParams = default;
            computeParams.x = (float)SystemDispatcher.CurrentUnscaledTimeSeconds;
            computeParams.y = ResolveDamageHologramScanlineWidth();
            computeParams.z = pointBudget;
            computeParams.w = _cheapVisualWeight01;
            damageHologramCompute.SetVector(DamageHologramParamsId, computeParams);
            damageHologramCompute.SetVector(DamageHologramBoundsId, _damageProxyBounds);
            damageHologramCompute.SetInt(DamageProxyVertexCountId, _damageProxyVertexCount);
            damageHologramCompute.SetInt(DamageRoomCountId, _damageRoomCount);

            damageHologramCompute.Dispatch(_damageHologramKernel, damageDispatchGroups, 1, 1);
            UpdateDamageHologramArgs(0, false);
            GraphicsBuffer.CopyCount(_damagePointBuffer, _damageArgsBuffer, 4);
            _damageHologramEstimatedPoints = _damageKnownActiveDentCount > 0 || _damageHologramHadSignal
                ? math.min(pointBudget, _damageProxyVertexCount)
                : math.max(1, math.min(pointBudget, _damageProxyVertexCount) >> 3);
        }

        private bool CanDispatchDamageHologramCompute()
        {
            return damageHologramCompute != null &&
                   SystemInfo.supportsComputeShaders &&
                   _damageHologramKernel >= 0 &&
                   damageHologramCompute.IsSupported(_damageHologramKernel) &&
                   _damageHologramThreadGroupSizeX > 0 &&
                   _activeDamageProxyVertexBuffer != null &&
                   _damageProxyVertexCount >= MinDamageProxyVertices &&
                   _activeDamageRoomWaterBuffer != null;
        }

        private int ResolveDamageHologramPointBudget()
        {
            float curve = SmoothQuality(_qualityWeight01);
            float continuous = math.lerp(FallbackDamageWarningPoints, MaxDamageHologramPoints, curve);
            return math.clamp((int)math.round(continuous), FallbackDamageWarningPoints, MaxDamageHologramPoints);
        }

        private void UploadFallbackDamageHologramGlyph()
        {
            _damageHologramUsingFallbackGlyph = true;
            bool warningActive = IsFallbackDamageWarningActive();
            if (!_damageHologramFallbackPointUploaded || _damageHologramFallbackWarningActive != warningActive)
            {
                if (warningActive)
                    FillFallbackWarningGlyph();
                else
                    FillFallbackIdleGlyph();
                _damagePointBuffer.SetData(_damageFallbackPoint, 0, 0, FallbackDamageWarningPoints);
                _damageHologramFallbackPointUploaded = true;
                _damageHologramFallbackWarningActive = warningActive;
            }

            UpdateDamageHologramArgs(FallbackDamageWarningPoints, false);
            _damageHologramEstimatedPoints = FallbackDamageWarningPoints;
        }

        private bool IsFallbackDamageWarningActive()
        {
            return _damageKnownActiveDentCount > 0 ||
                   _damageHologramFlood01 > 0.01f ||
                   _damageHologramFlickerTimer > 0f;
        }

        private void FillFallbackWarningGlyph()
        {
            _damageFallbackPoint[0] = new Vector4(0f, 0.24f, 0f, 0.72f);
            _damageFallbackPoint[1] = new Vector4(0f, 0.16f, 0f, 0.72f);
            _damageFallbackPoint[2] = new Vector4(0f, 0.08f, 0f, 0.72f);
            _damageFallbackPoint[3] = new Vector4(0f, 0.0f, 0f, 0.72f);
            _damageFallbackPoint[4] = new Vector4(-0.06f, -0.12f, 0f, 0.72f);
            _damageFallbackPoint[5] = new Vector4(0f, -0.12f, 0f, 0.72f);
            _damageFallbackPoint[6] = new Vector4(0.06f, -0.12f, 0f, 0.72f);
        }

        private void FillFallbackIdleGlyph()
        {
            _damageFallbackPoint[0] = new Vector4(-0.24f, 0f, 0f, -1f);
            _damageFallbackPoint[1] = new Vector4(-0.16f, 0.03f, 0f, -1f);
            _damageFallbackPoint[2] = new Vector4(-0.08f, 0f, 0f, -1f);
            _damageFallbackPoint[3] = new Vector4(0f, -0.03f, 0f, -1f);
            _damageFallbackPoint[4] = new Vector4(0.08f, 0f, 0f, -1f);
            _damageFallbackPoint[5] = new Vector4(0.16f, 0.03f, 0f, -1f);
            _damageFallbackPoint[6] = new Vector4(0.24f, 0f, 0f, -1f);
        }

        private void BindDamageHologramMaterial(Matrix4x4 hologramLocalToWorld)
        {
            if (!_damageHologramMaterialBufferBound)
            {
                _damageRuntimeMaterial.SetBuffer(DamageHologramPointsId, _damagePointBuffer);
                _damageRuntimeMaterial.SetBuffer(DamageRoomWaterLevelsId, _activeDamageRoomWaterBuffer);
                _damageHologramMaterialBufferBound = true;
            }

            _damageRuntimeMaterial.SetMatrix(DamageHologramLocalToWorldId, hologramLocalToWorld);
            Vector4 materialParams = default;
            materialParams.x = (float)SystemDispatcher.CurrentUnscaledTimeSeconds;
            materialParams.y = ResolveDamageHologramAlpha();
            materialParams.z = _damageRoomCount;
            materialParams.w = _cheapVisualWeight01;
            _damageRuntimeMaterial.SetVector(DamageHologramParamsId, materialParams);
            _damageRuntimeMaterial.SetVector(DamageHologramBoundsId, _damageProxyBounds);
            _damageRuntimeMaterial.SetFloat(DamageHologramFlickerId, ResolveDamageHologramFlicker());
        }

        private void UpdateDamageHologramArgs(int instanceCount, bool force)
        {
            Mesh mesh = ResolveDamagePointMesh();
            if (_damageArgsBuffer == null || mesh == null)
                return;

            int safeInstanceCount = math.max(0, instanceCount);
            if (!force &&
                ReferenceEquals(mesh, _lastDamageArgsMesh) &&
                _lastDamageArgsInstanceCount == safeInstanceCount)
            {
                return;
            }

            _damageHologramArgsUpload[0] = new GraphicsBuffer.IndirectDrawIndexedArgs
            {
                indexCountPerInstance = mesh.GetIndexCount(0),
                instanceCount = (uint)safeInstanceCount,
                startIndex = mesh.GetIndexStart(0),
                baseVertexIndex = (uint)Mathf.Max(0, mesh.GetBaseVertex(0)),
                startInstance = 0u
            };
            _damageArgsBuffer.SetData(_damageHologramArgsUpload, 0, 0, 1);
            _lastDamageArgsMesh = mesh;
            _lastDamageArgsInstanceCount = safeInstanceCount;
        }

        private Mesh ResolveDamagePointMesh()
        {
            return damagePointMesh != null ? damagePointMesh : _runtimeDamageCube;
        }

        private static Mesh CreateDamageCubeMesh()
        {
            Mesh mesh = new Mesh
            {
                name = "VSOS_DamageHologramCube"
            };
            mesh.SetVertices(DamageCubeVertices);
            mesh.SetIndices(DamageCubeIndices, MeshTopology.Triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);
            return mesh;
        }

        private float ResolveDamageHologramAlpha()
        {
            float power = SaturateFinite(_nodeVoltageSupplyRatio, 0f);
            return math.saturate(math.lerp(0.08f, 1f, power) * (1f - ResolveDamageHologramFlicker() * 0.35f));
        }

        private float ResolveDamageHologramFlicker()
        {
            if (_damageHologramFlickerTimer <= 0f)
                return 0f;

            float normalized = math.saturate(_damageHologramFlickerTimer * DamageHologramFlickerSecondsInv);
            return normalized * Hash01(Hecton8.Core.SystemDispatcher.CurrentFrameId ^ _damageHologramFlickerSeed);
        }

        private static float Hash01(uint value)
        {
            value ^= value >> 16;
            value *= 0x7feb352du;
            value ^= value >> 15;
            value *= 0x846ca68bu;
            value ^= value >> 16;
            return (value & 0x00ffffffu) * Hash24Inv;
        }

        private bool TryResolveGroundRadarRenderBinding(out IGroundRadarService groundRadar, out GraphicsBuffer buffer)
        {
            groundRadar = _cachedGroundRadar;
            if (groundRadar != null && groundRadar.TryGetGprPingBuffer(out buffer, out int activeCount, out _) && activeCount > 0)
                return true;

            buffer = null;
            return false;
        }

        private Mesh ResolveRadarMesh()
        {
            return radarBlipMesh != null ? radarBlipMesh : _runtimeRadarQuad;
        }

        private bool IsRadarDrawableReady()
        {
            return _radarRuntimeMaterial != null && ResolveRadarMesh() != null;
        }

        private static Mesh CreateRadarQuadMesh()
        {
            Mesh mesh = new Mesh
            {
                name = "VSOS_RadarBlipQuad"
            };
            mesh.SetVertices(RadarQuadVertices);
            mesh.SetUVs(0, RadarQuadUvs);
            mesh.SetIndices(RadarQuadIndices, MeshTopology.Triangles, 0);
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);
            return mesh;
        }

        private void ScheduleButtonJob(float deltaTime)
        {
            int safeButtonCount = ResolveButtonCount();
            if (_buttonJobScheduled || (!_buttonAnimationActive && !_buttonUploadDirty) ||
                !TryAcquireButtonJobBuffers(
                    safeButtonCount,
                    out NativeArray<byte> states,
                    out NativeArray<byte> targets,
                    out NativeArray<float> progress,
                    out NativeArray<float> offsets,
                    out NativeArray<CockpitButtonBasePosition> baseLocalPositions,
                    out NativeArray<float4x4> matrices))
            {
                return;
            }

            ButtonKinematicJob job = new ButtonKinematicJob
            {
                States = states,
                Targets = targets,
                Progress = progress,
                Offsets = offsets,
                BaseLocalPositions = baseLocalPositions,
                Matrices = matrices,
                DeltaTime = math.max(0f, deltaTime),
                TravelSecondsInv = ButtonTravelSecondsInv,
                PressedLocalZ = ResolvePressedLocalZ(),
                ButtonScale = new float3(1f)
            };
            try
            {
                _buttonJobHandle = job.Schedule(safeButtonCount, 8);
                _buttonJobScheduled = true;
            }
            catch
            {
                ReleaseButtonJobBufferLocks();
                throw;
            }
        }

        private void UploadButtonMatrices()
        {
            GraphicsBuffer buttonWriteBuffer = ResolveButtonMatrixWriteBuffer();
            if (buttonWriteBuffer == null ||
                !TryReadButtonMatrices(ResolveButtonCount(), out NativeArray<float4x4> buttonMatrices))
            {
                return;
            }

            GraphicsBufferUploadUtility.UploadNativeArray(buttonWriteBuffer, buttonMatrices, ResolveButtonCount());
            _activeButtonMatrixBuffer = buttonWriteBuffer;
            _buttonMatrixUploadIndex ^= 1;
        }

        private void ApplyButtonTransforms()
        {
            int safeButtonCount = ResolveButtonCount();
            if (buttonTransforms == null)
                return;
            if (!TryReadButtonBaseLocalPositions(safeButtonCount, out NativeArray<CockpitButtonBasePosition>.ReadOnly baseLocalPositions) ||
                !TryReadButtonOffsets(safeButtonCount, out NativeArray<float>.ReadOnly offsets))
            {
                return;
            }

            int transformCount = math.min(safeButtonCount, buttonTransforms.Length);
            for (int i = 0; i < transformCount; i++)
            {
                Transform button = buttonTransforms[i];
                if (button == null)
                    continue;

                float3 basePosition = baseLocalPositions[i].LocalPosition;
                basePosition.z += offsets[i];
                button.localPosition = (Vector3)basePosition;
            }
        }

        private void PressCockpitButton(int buttonIndex)
        {
            int safeButtonCount = ResolveButtonCount();
            if ((uint)buttonIndex >= (uint)safeButtonCount ||
                !TryAcquireButtonStateWriteBuffers(safeButtonCount, out NativeArray<byte> states, out NativeArray<byte> targets))
            {
                return;
            }

            byte desired;
            try
            {
                byte state = states[buttonIndex];
                byte target = targets[buttonIndex];
                desired = state == 2 || (state == 1 && target == 2) ? (byte)0 : (byte)2;
                targets[buttonIndex] = desired;
                states[buttonIndex] = 1;
            }
            finally
            {
                ReleaseButtonStateWriteBuffers();
            }

            _buttonAnimationActive = true;
            _buttonUploadDirty = true;
            _cockpitInteractions++;
            _screenDirty = true;

            if (buttonIndex == externalFeedLeverButtonIndex)
            {
                _externalFeedRequested = desired == 2;
                RequestOffscreenUiRender();
            }

            GlobalTelemetryBus.PublishPerformanceWarning(InteractionHash, TelemetryContextHash, _cockpitInteractions);
        }

        private int ResolveButtonCount()
        {
            int gridCapacity = math.min(MaxButtons, ResolveButtonColumns() * ResolveButtonRows());
            return math.clamp(buttonCount <= 0 ? gridCapacity : buttonCount, 1, MaxButtons);
        }

        private float3 ResolveButtonGridLocalPosition(int index)
        {
            int columns = ResolveButtonColumns();
            int rows = ResolveButtonRows();
            int column = index % columns;
            int row = index / columns;
            float2 halfExtents = ResolvePanelHalfExtents();
            float x = columns > 1 ? math.lerp(-halfExtents.x, halfExtents.x, column / (float)(columns - 1)) : 0f;
            float y = rows > 1 ? math.lerp(halfExtents.y, -halfExtents.y, row / (float)(rows - 1)) : 0f;
            return new float3(x, y, 0f);
        }

        private int ResolveButtonColumns()
        {
            return math.clamp(buttonColumns, 1, MaxButtons);
        }

        private int ResolveButtonRows()
        {
            return math.clamp(buttonRows, 1, MaxButtons);
        }

        private float ResolveNodeVoltageSupplyRatio()
        {
            IPowerGridService powerGrid = _cachedPowerGrid;
            if (powerGrid != null &&
                powerGrid.TryGetGridPowerPotentialsReadOnly(math.max(0, submarinePowerGridIndex), out NativeArray<float>.ReadOnly potentials) &&
                (uint)submarineNodeVoltageIndex < (uint)potentials.Length)
            {
                return SaturateFinite(potentials[submarineNodeVoltageIndex], _latestPowerRatio);
            }

            return SaturateFinite(_latestPowerRatio, 1f);
        }

        private void ApplyScreenMaterial()
        {
            if (centralScreenRenderer == null)
                return;

            Texture activeTexture = ResolveActiveScreenTexture();
            float power = SaturateFinite(_nodeVoltageSupplyRatio, 0f);
            float externalFeedBlend = ResolveExternalFeedBlend();
            if (ReferenceEquals(activeTexture, _lastScreenTexture) &&
                math.abs(power - _lastScreenPower) < 0.005f &&
                math.abs(externalFeedBlend - _lastExternalFeedBlend) < 0.005f)
            {
                return;
            }

            centralScreenRenderer.GetPropertyBlock(_screenPropertyBlock);
            if (activeTexture != null)
            {
                _screenPropertyBlock.SetTexture(MainTexId, activeTexture);
                _screenPropertyBlock.SetTexture(BaseMapId, activeTexture);
            }

            float dim = math.lerp(0.04f, 1f, power);
            _screenPropertyBlock.SetColor(BaseColorId, new Color(dim, dim, dim, 1f));
            _screenPropertyBlock.SetFloat(PanelPowerLevelId, power);
            _screenPropertyBlock.SetFloat(ExternalFeedBlendId, externalFeedBlend);
            centralScreenRenderer.SetPropertyBlock(_screenPropertyBlock);
            _lastScreenTexture = activeTexture;
            _lastScreenPower = power;
            _lastExternalFeedBlend = externalFeedBlend;
        }

        private float ResolveExternalFeedBlend()
        {
            return (_externalFeedRequested || _externalFeedActive) ? math.saturate(_externalFeedWeight01) : 0f;
        }

        private Texture ResolveActiveScreenTexture()
        {
            if (_externalFeedWeight01 <= 0.0001f && _externalFeedRequested && staticExternalNoiseTexture != null)
                return staticExternalNoiseTexture;
            if (_externalFeedActive && _externalRenderTexture != null)
                return _externalRenderTexture;
            return _uiRenderTexture;
        }

        private void RecordTelemetry()
        {
            if (!TryAcquireTelemetryWriteBuffer(out NativeArray<CockpitTelemetryEntry> telemetryRing))
                return;

            Transform anchor = radarDomeAnchor != null ? radarDomeAnchor : transform;
            Vector3 position = anchor.position;
            bool positionFinite = IsFinite(position);
            float holoFlicker = ResolveDamageHologramFlicker();
            bool finite = positionFinite &&
                          math.isfinite(_nodeVoltageSupplyRatio) &&
                          math.isfinite(_latestOxygenNormalized) &&
                          math.isfinite(_latestCarbonDioxideNormalized) &&
                          math.isfinite(_latestSpeedKnots) &&
                          math.isfinite(_damageHologramFlood01) &&
                          math.isfinite(holoFlicker);
            Vector3 safePosition = positionFinite ? position : Vector3.zero;
            int slot = _telemetryWriteIndex;
            bool shouldDump = false;
            bool shouldPublish = false;
            try
            {
                telemetryRing[slot] = new CockpitTelemetryEntry
                {
                    Frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex,
                    RadarActivePoints = _radarActivePoints,
                    CockpitInteractions = _cockpitInteractions,
                    Flags = BuildTelemetryFlags(finite),
                    Power = SaturateFinite(_nodeVoltageSupplyRatio, 0f),
                    Oxygen = SaturateFinite(_latestOxygenNormalized, 0f),
                    Co2 = SaturateFinite(_latestCarbonDioxideNormalized, 0f),
                    SpeedKnots = math.isfinite(_latestSpeedKnots) ? _latestSpeedKnots : 0f,
                    AnchorPosition = safePosition,
                    HoloDamagePoints = _damageHologramEstimatedPoints,
                    HoloProxyVertices = _damageProxyVertexCount,
                    HoloFlicker = SaturateFinite(holoFlicker, 0f),
                    HoloFlood01 = SaturateFinite(_damageHologramFlood01, 0f),
                    HoloFlags = BuildDamageHologramTelemetryFlags()
                };
                _telemetryCursor++;
                _telemetryWriteIndex++;
                if (_telemetryWriteIndex >= TelemetryCapacity)
                    _telemetryWriteIndex = 0;

                if (!finite && _nanDumped == 0)
                {
                    _nanDumped = 1;
                    shouldDump = true;
                }

                if (Hecton8.Core.SystemDispatcher.CurrentFrameIndex >= _telemetryPublishFrame)
                {
                    _telemetryPublishFrame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex + 30;
                    shouldPublish = true;
                }
            }
            finally
            {
                ReleaseTelemetryWriteBuffer();
            }

            if (shouldDump)
                DumpBlackbox();

            if (shouldPublish)
                GlobalTelemetryBus.PublishPerformanceWarning(RadarActiveHash, TelemetryContextHash, _radarActivePoints);
        }

        private uint BuildTelemetryFlags(bool finite)
        {
            uint flags = 0u;
            if (_radarPowered)
                flags |= 1u;
            if (_externalFeedActive)
                flags |= 2u;
            if (_cheapVisualWeight01 > 0.001f)
                flags |= 4u;
            if (!finite)
                flags |= 0x80000000u;
            return flags;
        }

        private uint BuildDamageHologramTelemetryFlags()
        {
            uint flags = 0u;
            if (_damageHologramResourcesReady)
                flags |= 1u;
            if (_cheapVisualWeight01 > 0.001f)
                flags |= 2u;
            if (_damageKnownActiveDentCount > 0)
                flags |= 4u;
            if (_damageHologramFlickerTimer > 0f)
                flags |= 8u;
            if (_damageHologramFlood01 > 0.01f)
                flags |= 16u;
            if (_damageHologramUsingFallbackGlyph && IsFallbackDamageWarningActive())
                flags |= 32u;
            return flags;
        }

        private void DumpBlackbox()
        {
            if (!TryReadTelemetryRing(out NativeArray<CockpitTelemetryEntry>.ReadOnly telemetryRing))
                return;

            try
            {
                string root = Directory.GetParent(Application.dataPath)?.FullName;
                if (string.IsNullOrEmpty(root))
                    return;

                string directory = Path.Combine(root, "Docs", "AgentLogs");
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, "Dump_VEHICLE_SUB_OS.bin");
                using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
                using BinaryWriter writer = new BinaryWriter(stream);
                writer.Write(TelemetryContextHash);
                writer.Write(_telemetryCursor);
                writer.Write(_telemetryWriteIndex);
                int entryCount = math.min(_telemetryCursor, TelemetryCapacity);
                writer.Write(entryCount);
                int readIndex = _telemetryCursor >= TelemetryCapacity ? _telemetryWriteIndex : 0;
                for (int i = 0; i < entryCount; i++)
                {
                    int slot = readIndex + i;
                    if (slot >= TelemetryCapacity)
                        slot -= TelemetryCapacity;

                    CockpitTelemetryEntry entry = telemetryRing[slot];
                    writer.Write(entry.Frame);
                    writer.Write(entry.RadarActivePoints);
                    writer.Write(entry.CockpitInteractions);
                    writer.Write(entry.Flags);
                    writer.Write(entry.Power);
                    writer.Write(entry.Oxygen);
                    writer.Write(entry.Co2);
                    writer.Write(entry.SpeedKnots);
                    writer.Write(entry.AnchorPosition.x);
                    writer.Write(entry.AnchorPosition.y);
                    writer.Write(entry.AnchorPosition.z);
                    writer.Write(entry.HoloDamagePoints);
                    writer.Write(entry.HoloProxyVertices);
                    writer.Write(entry.HoloFlicker);
                    writer.Write(entry.HoloFlood01);
                    writer.Write(entry.HoloFlags);
                }

                WriteDamageHolographerMirrorDump(directory, entryCount, telemetryRing);
            }
            catch (Exception)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(0x44554D50u, TelemetryContextHash, 1f);
            }
        }

        private void WriteDamageHolographerMirrorDump(
            string directory,
            int entryCount,
            NativeArray<CockpitTelemetryEntry>.ReadOnly telemetryRing)
        {
            string path = Path.Combine(directory, "Dump_DIEGETIC_DAMAGE_HOLOGRAPHER.bin");
            using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
            using BinaryWriter writer = new BinaryWriter(stream);
            writer.Write(DamageHologramTelemetryHash);
            writer.Write(_telemetryCursor);
            writer.Write(_telemetryWriteIndex);
            writer.Write(entryCount);
            int readIndex = _telemetryCursor >= TelemetryCapacity ? _telemetryWriteIndex : 0;
            for (int i = 0; i < entryCount; i++)
            {
                int slot = readIndex + i;
                if (slot >= TelemetryCapacity)
                    slot -= TelemetryCapacity;

                CockpitTelemetryEntry entry = telemetryRing[slot];
                writer.Write(entry.Frame);
                writer.Write(entry.HoloDamagePoints);
                writer.Write(entry.HoloProxyVertices);
                writer.Write(entry.HoloFlicker);
                writer.Write(entry.HoloFlood01);
                writer.Write(entry.HoloFlags);
            }
        }

        private bool HasButtonTransitions()
        {
            if (!TryReadButtonStates(ResolveButtonCount(), out NativeArray<byte>.ReadOnly buttonStates))
                return false;

            int safeButtonCount = ResolveButtonCount();
            for (int i = 0; i < safeButtonCount; i++)
            {
                if (buttonStates[i] == 1)
                    return true;
            }

            return false;
        }

        private void CompleteButtonJobForTeardown()
        {
            if (!_buttonJobScheduled)
                return;

            DispatcherJobSwap.TryComplete(ref _buttonJobHandle, forceComplete: true);
            _buttonJobScheduled = false;
            ReleaseButtonJobBufferLocks();
        }

        private static bool IsFinite(Vector3 value)
        {
            return math.isfinite(value.x) &&
                   math.isfinite(value.y) &&
                   math.isfinite(value.z);
        }

        private static bool IsFinite(Vector4 value)
        {
            return math.isfinite(value.x) &&
                   math.isfinite(value.y) &&
                   math.isfinite(value.z) &&
                   math.isfinite(value.w);
        }

        private static bool IsFinite(Matrix4x4 value)
        {
            return IsFinite(value.GetColumn(0)) &&
                   IsFinite(value.GetColumn(1)) &&
                   IsFinite(value.GetColumn(2)) &&
                   IsFinite(value.GetColumn(3));
        }

        private static float SaturateFinite(float value, float fallback)
        {
            return math.isfinite(value) ? math.saturate(value) : math.isfinite(fallback) ? math.saturate(fallback) : 0f;
        }

        private float2 ResolvePanelHalfExtents()
        {
            float x = math.isfinite(panelHalfExtents.x) ? math.max(0.001f, panelHalfExtents.x) : 0.72f;
            float y = math.isfinite(panelHalfExtents.y) ? math.max(0.001f, panelHalfExtents.y) : 0.36f;
            return new float2(x, y);
        }

        private float ResolvePressedLocalZ()
        {
            return math.isfinite(buttonPressedLocalZ) ? math.clamp(buttonPressedLocalZ, -0.08f, 0.02f) : -0.035f;
        }

        private float ResolveRadarRadiusMeters()
        {
            return math.isfinite(radarRadiusMeters) ? math.max(0.001f, radarRadiusMeters) : 0.42f;
        }

        private float ResolveRadarBoundsSize()
        {
            return math.isfinite(radarBoundsSizeMeters) ? math.max(0.1f, radarBoundsSizeMeters) : 1.2f;
        }

        private float ResolveDamageHologramBoundsSize()
        {
            return math.isfinite(damageHologramBoundsSizeMeters) ? math.max(0.1f, damageHologramBoundsSizeMeters) : 1f;
        }

        private float ResolveDamageHologramScanlineWidth()
        {
            return math.isfinite(damageHologramScanlineWidth) ? math.clamp(damageHologramScanlineWidth, 0.02f, 0.35f) : 0.11f;
        }

        private static int ResolveKernelThreadGroupSizeX(
            ComputeShader compute,
            int kernel)
        {
            if (compute == null || kernel < 0 || !SystemInfo.supportsComputeShaders || !compute.IsSupported(kernel))
                return 0;

            compute.GetKernelThreadGroupSizes(kernel, out uint sizeX, out uint sizeY, out uint sizeZ);
            if (sizeX == 0u || sizeY != 1u || sizeZ != 1u || sizeX > int.MaxValue)
                return 0;

            ulong totalThreads = sizeX * (ulong)sizeY * sizeZ;
            return totalThreads <= PortableMaxComputeThreadsPerGroup ? (int)sizeX : 0;
        }

        private static int ResolveSupportedKernel(ComputeShader compute, string kernelName)
        {
            if (compute == null || !SystemInfo.supportsComputeShaders || !compute.HasKernel(kernelName))
                return -1;

            int kernel = compute.FindKernel(kernelName);
            return kernel >= 0 && compute.IsSupported(kernel) ? kernel : -1;
        }

        private static int CeilDividePositive(int value, int divisor)
        {
            const int MaxDispatchGroupsPerDimension = 65535;
            if (value <= 0 || divisor <= 0)
                return 0;

            long groups = ((long)value + divisor - 1L) / divisor;
            return groups <= MaxDispatchGroupsPerDimension ? (int)groups : 0;
        }

        private void OnValidate()
        {
            ResolveColdAssetReferences();
            buttonColumns = ResolveButtonColumns();
            buttonRows = ResolveButtonRows();
            buttonCount = math.clamp(buttonCount, 1, MaxButtons);
            externalFeedLeverButtonIndex = math.clamp(externalFeedLeverButtonIndex, 0, ResolveButtonCount() - 1);
            float2 extents = ResolvePanelHalfExtents();
            panelHalfExtents.x = extents.x;
            panelHalfExtents.y = extents.y;
            buttonPressedLocalZ = ResolvePressedLocalZ();
            radarRadiusMeters = ResolveRadarRadiusMeters();
            radarBoundsSizeMeters = ResolveRadarBoundsSize();
            damageHologramBoundsSizeMeters = ResolveDamageHologramBoundsSize();
            damageHologramScanlineWidth = ResolveDamageHologramScanlineWidth();
            uiRenderTextureWidth = math.max(MinUiRenderTextureWidth, uiRenderTextureWidth);
            uiRenderTextureHeight = math.max(MinUiRenderTextureHeight, uiRenderTextureHeight);
            externalRenderTextureWidth = math.max(MinExternalRenderTextureWidth, externalRenderTextureWidth);
            externalRenderTextureHeight = math.max(MinExternalRenderTextureHeight, externalRenderTextureHeight);
            _buttonBasesInitialized = false;
        }

        private void Reset()
        {
            OnValidate();
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct ButtonKinematicJob : IJobParallelFor
        {
            [NoAlias]
            public NativeArray<byte> States;
            [NoAlias]
            public NativeArray<byte> Targets;
            [NoAlias]
            public NativeArray<float> Progress;
            [NoAlias]
            public NativeArray<float> Offsets;
            [ReadOnly, NoAlias]
            public NativeArray<CockpitButtonBasePosition> BaseLocalPositions;
            [NoAlias]
            public NativeArray<float4x4> Matrices;
            public float DeltaTime;
            public float TravelSecondsInv;
            public float PressedLocalZ;
            public float3 ButtonScale;

            public void Execute(int index)
            {
                byte state = States[index];
                float progressValue = Progress[index];
                float progress = math.isfinite(progressValue) ? math.saturate(progressValue) : 0f;
                float pressedLocalZ = math.isfinite(PressedLocalZ) ? PressedLocalZ : -0.035f;
                if (state == 1)
                {
                    float target = Targets[index] == 2 ? 1f : 0f;
                    float step = math.max(0f, DeltaTime) * math.max(0f, TravelSecondsInv);
                    progress = MoveTowards(progress, target, step);
                    if (math.abs(progress - target) <= 0.0001f)
                    {
                        progress = target;
                        States[index] = Targets[index];
                    }
                }
                else
                {
                    progress = state == 2 ? 1f : 0f;
                }

                Progress[index] = progress;
                float offset = progress * pressedLocalZ;
                Offsets[index] = offset;
                float3 position = BaseLocalPositions[index].LocalPosition;
                position.z += offset;
                Matrices[index] = float4x4.TRS(position, quaternion.identity, ButtonScale);
            }

            private static float MoveTowards(float current, float target, float maxDelta)
            {
                float delta = target - current;
                if (math.abs(delta) <= maxDelta)
                    return target;
                return current + math.sign(delta) * maxDelta;
            }
        }

        [StructLayout(LayoutKind.Explicit, Size = 32)]
        private struct RadarBlipGpuData
        {
            [FieldOffset(0)]
            public float4 LocalPositionSize;
            [FieldOffset(16)]
            public float4 ColorAlpha;
        }

        [StructLayout(LayoutKind.Explicit, Size = 16)]
        private struct CockpitButtonBasePosition
        {
            [FieldOffset(0)]
            public float3 LocalPosition;
            [FieldOffset(12)]
            private uint _pad0;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct CockpitTelemetryEntry
        {
            [FieldOffset(0)]
            public int Frame;
            [FieldOffset(4)]
            public int RadarActivePoints;
            [FieldOffset(8)]
            public int CockpitInteractions;
            [FieldOffset(12)]
            public uint Flags;
            [FieldOffset(16)]
            public float Power;
            [FieldOffset(20)]
            public float Oxygen;
            [FieldOffset(24)]
            public float Co2;
            [FieldOffset(28)]
            public float SpeedKnots;
            [FieldOffset(32)]
            public Vector3 AnchorPosition;
            [FieldOffset(44)]
            public int HoloDamagePoints;
            [FieldOffset(48)]
            public int HoloProxyVertices;
            [FieldOffset(52)]
            public float HoloFlicker;
            [FieldOffset(56)]
            public float HoloFlood01;
            [FieldOffset(60)]
            public uint HoloFlags;
        }
    }
}
