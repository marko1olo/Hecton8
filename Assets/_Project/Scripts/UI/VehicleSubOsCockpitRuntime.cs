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
using Hecton8.SaveSystem;
using Hecton8.World;
using TMPro;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.UI
{
    /// <summary>
    /// Dispatcher-owned diegetic submarine cockpit bridge: analytical controls, off-screen screens, and GPU sonar radar.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VehicleSubOsCockpitRuntime : MonoBehaviour, IColdTickable, ILateFrameTickable, ISlowTickable, IRenderable, ISubmarineOsEventListener, IPowerGridTelemetryListener, IGlobalRegistryHotSwapListener
    {
        private const int MaxRadarPoints = 4096;
        private const int MinQualityRadarPoints = 512;
        private const int MaxRadarPointsPerTap = 256;
        private const int MinQualityRadarPointsPerTap = 32;
        private const float CheapVisualQualityThreshold = 0.3f;
        private const float CheapVisualQualityRampInv = 5.5555553f;
        private const float MinExternalFeedBlendWeight = 0.125f;
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
        private const string DamageHologramKernelName = "KMapHullDents";
        private const uint PortableMaxComputeThreadsPerGroup = 256u;
        private const uint TelemetryContextHash = 0x56534F53u; // VSOS
        private const uint DamageHologramTelemetryHash = 0x44484F4Cu; // DHOL
        private const uint RadarActiveHash = 0x52414452u; // RADR
        private const uint InteractionHash = 0x42544E53u; // BTNS
        private const int DumpHeaderBytes = 16;
        private const int CockpitTelemetryDumpEntryBytes = 64;
        private const int DamageHologramDumpEntryBytes = 24;
        private const string DamageHolographerMirrorDumpPath = "Docs/AgentLogs/Dump_VEHICLE_SUB_OS_DAMAGE_HOLOGRAPHER.bin";
        private const string DamageHologramDumpPayloadLabel = "vehicleSubOsDamageHologramDumpPayload";
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
        [SerializeField] private GameObject _authoredCockpitPanelPrefab;
        [SerializeField] private Material _sharedUiMaterial;
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
        private readonly byte[] _buttonStateScratch = new byte[MaxButtons]; // COLD ALLOC: byte[32] - cockpit button state staging - owner: VehicleSubOsCockpitRuntime
        private readonly float[] _buttonProgressScratch = new float[MaxButtons]; // COLD ALLOC: float[32] - cockpit button progress staging - owner: VehicleSubOsCockpitRuntime
        private readonly float[] _buttonOffsetScratch = new float[MaxButtons]; // COLD ALLOC: float[32] - cockpit button offset staging - owner: VehicleSubOsCockpitRuntime
        private readonly CockpitButtonBasePosition[] _buttonBaseScratch = new CockpitButtonBasePosition[MaxButtons]; // COLD ALLOC: button base[32] - cockpit button base staging - owner: VehicleSubOsCockpitRuntime
        private readonly float4x4[] _buttonMatrixScratch = new float4x4[MaxButtons]; // COLD ALLOC: matrix[32] - cockpit button matrix staging - owner: VehicleSubOsCockpitRuntime

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
        private GraphicsBuffer _damagePointUploadBuffer;
        private GraphicsBuffer _damageArgsUploadBuffer;
        private GraphicsBuffer _damageRoomWaterBufferA;
        private GraphicsBuffer _damageRoomWaterBufferB;
        private GraphicsBuffer _activeDamageRoomWaterBuffer;
        private MaterialPropertyBlock _screenPropertyBlock;
        private MaterialPropertyBlock _radarMaterialProperties;
        private MaterialPropertyBlock _damageHologramProperties;
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

        private bool _registeredLateFrame;
        private bool _registeredSlowTick;
        private bool _registeredColdTick;
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
        private bool _presentationResourcesDirty = true;
        private bool _renderTargetsDirty = true;
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
        private bool _supportsComputeShadersCold;
        private bool _supportsRgb565RenderTextureCold;
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
            CacheGraphicsCapabilitiesCold();
            CacheRegistryServicesCold();
            RefreshQualityPolicy(allowGraphicsResourceMutation: true);
            EnsureMaterialPropertyBlocksCold();
            EnsureNativeResources();
            EnsureGraphicsResources();
            EnsureRenderTargets();
            BindAuthoredCockpitPanelCold();
        }

        private void OnEnable()
        {
            InvalidateOffscreenTextCache();
            CacheGraphicsCapabilitiesCold();
            CacheRegistryServicesCold();
            RefreshQualityPolicy(allowGraphicsResourceMutation: true);
            EnsureMaterialPropertyBlocksCold();
            EnsureNativeResources();
            EnsureGraphicsResources();
            EnsureRenderTargets();
            BindAuthoredCockpitPanelCold();
            HectonSubmarineOsEvents.Register(this);
            PowerGridTelemetryEvents.Register(this);
            TryRegisterHotSwapListener();
            TryRegisterRuntime();
            ApplyScreenMaterial();
            ApplyOffscreenUiCameraState();
        }

        private void OnDisable()
        {
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
            TryUnregisterHotSwapListener();
            ReleaseExternalRenderTexture();
            DisposeGraphicsResources();
            DisposeNativeResources();
            ReleaseUiRenderTexture();
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
            UpdateButtonKinematics(safeDeltaTime);
            RecordTelemetry();
        }

        public void LateFrameTick()
        {
            AdvanceCockpitFrameState(SystemDispatcher.CurrentFrameDeltaTime);

            if (_resourceRefreshDirty || !_resourcesReady)
            {
                RefreshNativeResourceReadinessForFrame();
            }

            bool presentationResourcesReady = HasPresentationResourcesReadyForFrame();
            if (_resourcesReady && presentationResourcesReady)
            {
                if (ShouldRetryRadarGraphicsResources())
                    ClearRadarDrawState();
                FlushDamageRoomWaterUpload();
                UploadSonarTapsAndDispatchRadar();
                if (UpdateOffscreenText(SystemDispatcher.CurrentFrameUnscaledDeltaTime))
                    RequestOffscreenUiRender();
                ApplyScreenMaterial();
                ApplyOffscreenUiCameraState();
            }

            if (_resourcesReady && (_buttonUploadDirty || _buttonAnimationActive))
            {
                if (presentationResourcesReady)
                    UploadButtonMatrices();
                ApplyButtonTransforms();
                _buttonAnimationActive = HasButtonTransitions();
                _buttonUploadDirty = false;
            }
        }

        public void SlowTick()
        {
            if (_resourceRefreshDirty || !_resourcesReady)
            {
                _resourceRefreshDirty = true;
                return;
            }

            if (!_resourcesReady)
                return;

            if (_graphicsResourceDisposalPending)
                return;

            if (_externalFeedStateDirty)
            {
                _externalFeedStateDirty = false;
                UpdateExternalFeedState();
            }

            if (_presentationResourcesDirty ||
                _renderTargetsDirty ||
                !AreRenderTargetsCurrent())
            {
                _resourceRefreshDirty = true;
            }
        }

        public void ColdTick()
        {
            if (!Application.isPlaying || !isActiveAndEnabled)
                return;

            if (_graphicsResourceDisposalPending)
            {
                _graphicsResourceDisposalPending = false;
                DisposeGraphicsResources();
            }

            if (_resourceRefreshDirty || !_resourcesReady)
            {
                _resourceRefreshDirty = false;
                EnsureNativeResources();
            }

            if (!_resourcesReady)
                return;

            if (_presentationResourcesDirty)
                EnsureGraphicsResources();

            if (_renderTargetsDirty || !AreRenderTargetsCurrent())
                EnsureRenderTargets();
        }

        public void Render(float deltaTime)
        {
            if (!HasPresentationResourcesReadyForFrame())
                return;

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
            if (!_registeredColdTick)
                _registeredColdTick = SystemDispatcher.Register((IColdTickable)this, PriorityLayer.UI);
            if (!_registeredLateFrame)
                _registeredLateFrame = SystemDispatcher.Register((ILateFrameTickable)this, PriorityLayer.UI);
            if (!_registeredSlowTick)
                _registeredSlowTick = SystemDispatcher.Register((ISlowTickable)this, PriorityLayer.UI);
            if (!_registeredRenderable)
                _registeredRenderable = GlobalRegistry.Renderables.TryRegister(this);
        }

        private void UnregisterRuntime()
        {
            if (_registeredColdTick)
            {
                SystemDispatcher.Unregister((IColdTickable)this, PriorityLayer.UI);
                _registeredColdTick = false;
            }

            if (_registeredSlowTick)
            {
                SystemDispatcher.Unregister((ISlowTickable)this, PriorityLayer.UI);
                _registeredSlowTick = false;
            }

            if (_registeredLateFrame)
            {
                SystemDispatcher.UnregisterLateFrameTickableDirect(this, PriorityLayer.UI);
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

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.RenderTexturePoolRuntime:
                    IRenderTexturePoolService nextPool = currentService as IRenderTexturePoolService;
                    bool externalPoolChanged =
                        _externalRenderTexture != null &&
                        !ReferenceEquals(_externalRenderTexturePoolOwner, nextPool);
                    if (externalPoolChanged)
                    {
                        ReleaseExternalRenderTexture();
                        _externalFeedActive = false;
                        _lastScreenTexture = null;
                    }

                    _cachedRenderTexturePool = nextPool;
                    if (externalPoolChanged && _externalFeedRequested && isActiveAndEnabled)
                        EnsureExternalRenderTextureCurrent();
                    break;
                case GlobalRegistryServiceSlot.PlayerCriticalAudioRuntime:
                    CachePlayerCriticalAudio(currentService as IPlayerCriticalSonarEchoReadModel);
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
                case GlobalRegistryServiceSlot.DataVault:
                    IDataVault previousVault = previousService is IDataVault oldVault ? oldVault : _dataVault;
                    IDataVault nextVault = currentService is IDataVault currentVault ? currentVault : null;
                    BindDataVaultForLifecycle(nextVault, previousVault);
                    _buttonBasesInitialized = false;
                    _resourcesReady = false;
                    if (isActiveAndEnabled && nextVault != null)
                        EnsureNativeResources();
                    _resourceRefreshDirty = isActiveAndEnabled && !_resourcesReady;
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
            CachePlayerCriticalAudio(GlobalRegistry.PlayerCriticalSonarEcho);
            _cachedGroundRadar = GlobalRegistry.GroundRadar;
            _cachedHabitatGraph = GlobalRegistry.HabitatGraph;
            _cachedPowerGrid = GlobalRegistry.PowerGrid;
            CacheDataVaultCold();
        }

        private void CachePlayerCriticalAudio(IPlayerCriticalSonarEchoReadModel playerCriticalAudio)
        {
            _cachedPlayerCriticalAudio = IsPlayerCriticalSonarEchoReadModelUsable(playerCriticalAudio)
                ? playerCriticalAudio
                : null;
        }

        private IPlayerCriticalSonarEchoReadModel ResolvePlayerCriticalSonarEchoReadModel()
        {
            IPlayerCriticalSonarEchoReadModel playerCriticalAudio = _cachedPlayerCriticalAudio;
            if (IsPlayerCriticalSonarEchoReadModelUsable(playerCriticalAudio))
                return playerCriticalAudio;

            _cachedPlayerCriticalAudio = null;
            return null;
        }

        private static bool IsPlayerCriticalSonarEchoReadModelUsable(IPlayerCriticalSonarEchoReadModel playerCriticalAudio)
        {
            if (playerCriticalAudio == null)
                return false;

            if (playerCriticalAudio is Behaviour behaviour)
                return behaviour != null && behaviour.isActiveAndEnabled;

            return true;
        }

        private void CacheGraphicsCapabilitiesCold()
        {
            _supportsComputeShadersCold = SystemInfo.supportsComputeShaders;
            _supportsRgb565RenderTextureCold = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RGB565);
        }

        private void RefreshQualityPolicy(bool allowGraphicsResourceMutation)
        {
            float quality = math.saturate(HomeostasisBrain.GlobalQualityWeight);
            int capacity = ResolveRadarCapacity(quality);
            int pointsPerTap = ResolveRadarPointsPerTap(quality);
            float cheapVisualWeight = ResolveCheapVisualWeight(quality);
            float externalFeedWeight = ResolveExternalFeedWeight(quality);
            RenderTextureFormat format = ResolvePanelRenderTextureFormat();
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
            _renderTargetsDirty = true;
            _screenDirty = true;
            if (capacity != _radarCapacity)
            {
                bool requiresRadarBufferGrow =
                    capacity > _radarCapacity &&
                    radarCompute != null &&
                    _supportsComputeShadersCold &&
                    !HasRadarBufferCapacity(capacity);
                _radarCapacity = capacity;
                if (requiresRadarBufferGrow)
                {
                    _radarResourcesReady = false;
                    if (allowGraphicsResourceMutation)
                        DisposeGraphicsResources();
                    else
                        _graphicsResourceDisposalPending = true;
                    _presentationResourcesDirty = true;
                }
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
            float curve = SmoothQuality(qualityWeight01);
            return math.lerp(MinExternalFeedBlendWeight, 1f, curve);
        }

        private static float SmoothQuality(float qualityWeight01)
        {
            float quality = math.saturate(qualityWeight01);
            return quality * quality * (3f - 2f * quality);
        }

        private void EnsureNativeResources()
        {
            int safeButtonCount = ResolveButtonCount();
            IDataVault vault = _dataVault;
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

            if ((recreated || !_buttonBasesInitialized) && TryWriteButtonBaseSnapshots(safeButtonCount))
            {
                _buttonBasesInitialized = true;
                _buttonUploadDirty = true;
                _buttonAnimationActive = true;
            }
            else if (recreated || !_buttonBasesInitialized)
            {
                _resourcesReady = false;
                return;
            }

            _resourcesReady = HasButtonNativeResources(safeButtonCount);
        }

        private void RefreshNativeResourceReadinessForFrame()
        {
            _resourcesReady = HasButtonNativeResources(ResolveButtonCount());
        }

        private void DisposeNativeResources()
        {
            ReleaseCockpitVaultHandles(_dataVault);
            _buttonBasesInitialized = false;
            _resourcesReady = false;
        }

        private IDataVault CacheDataVaultCold()
        {
            if (_dataVault == null)
                BindDataVaultForLifecycle(GlobalRegistry.DataVault);

            return _dataVault;
        }

        private void BindDataVaultForLifecycle(IDataVault nextVault, IDataVault previousVault = null)
        {
            IDataVault releaseVault = previousVault ?? _dataVault;
            if (!ReferenceEquals(_dataVault, nextVault))
                ReleaseCockpitVaultHandles(releaseVault);

            _dataVault = nextVault;
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

            if (IsExactVaultHandle(in handle, bufferId))
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

        private bool TryWriteButtonBaseSnapshots(int safeButtonCount)
        {
            int count = math.clamp(safeButtonCount, 0, MaxButtons);
            for (int i = 0; i < count; i++)
            {
                Transform button = buttonTransforms != null && i < buttonTransforms.Length ? buttonTransforms[i] : null;
                float3 fallbackPosition = ResolveButtonGridLocalPosition(i);
                Vector3 baseVector = button != null
                    ? button.localPosition
                    : new Vector3(fallbackPosition.x, fallbackPosition.y, fallbackPosition.z);
                float3 basePosition = IsFinite(baseVector) ? new float3(baseVector.x, baseVector.y, baseVector.z) : fallbackPosition;
                _buttonBaseScratch[i] = new CockpitButtonBasePosition { LocalPosition = basePosition };
                _buttonMatrixScratch[i] = float4x4.TRS(basePosition, quaternion.identity, new float3(1f));
            }

            return TryWriteCockpitVaultBuffer(in _buttonBaseLocalPositionsHandle, ButtonBaseLocalPositionsBufferId, count, _buttonBaseScratch) &&
                   TryWriteCockpitVaultBuffer(in _buttonMatricesHandle, ButtonMatricesBufferId, count, _buttonMatrixScratch);
        }

        private bool TryWriteButtonByteValue(
            in VaultGenerationHandle<byte> handle,
            BufferID bufferId,
            int requiredButtonCount,
            int buttonIndex,
            byte value)
        {
            IDataVault vault = _dataVault;
            NativeArray<byte> buffer = default;
            bool locked = false;
            try
            {
                if (vault == null ||
                    (uint)buttonIndex >= (uint)requiredButtonCount ||
                    !IsExactVaultHandle(in handle, bufferId) ||
                    !vault.TryAcquireWriteLock(in handle, VaultOwnerSystemId, out buffer))
                {
                    return false;
                }

                locked = true;
                if (!buffer.IsCreated || buffer.Length < requiredButtonCount)
                    return false;

                buffer[buttonIndex] = value;
                return true;
            }
            finally
            {
                if (locked)
                    vault.ReleaseWriteLock(in handle, VaultOwnerSystemId);
            }
        }

        private bool TryWriteCockpitVaultBuffer<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int count,
            T[] source)
            where T : unmanaged
        {
            IDataVault vault = _dataVault;
            NativeArray<T> buffer = default;
            bool locked = false;
            try
            {
                if (vault == null ||
                    source == null ||
                    count < 0 ||
                    source.Length < count ||
                    !IsExactVaultHandle(in handle, bufferId) ||
                    !vault.TryAcquireWriteLock(in handle, VaultOwnerSystemId, out buffer))
                {
                    return false;
                }

                locked = true;
                if (!buffer.IsCreated || buffer.Length < count)
                    return false;

                for (int i = 0; i < count; i++)
                    buffer[i] = source[i];

                return true;
            }
            finally
            {
                if (locked)
                    vault.ReleaseWriteLock(in handle, VaultOwnerSystemId);
            }
        }

        private bool TryWriteTelemetryEntry(int slot, in CockpitTelemetryEntry entry)
        {
            IDataVault vault = _dataVault;
            NativeArray<CockpitTelemetryEntry> telemetryRing = default;
            bool locked = false;
            try
            {
                if (vault == null ||
                    (uint)slot >= (uint)TelemetryCapacity ||
                    !IsExactVaultHandle(in _telemetryRingHandle, TelemetryRingBufferId) ||
                    !vault.TryAcquireWriteLock(in _telemetryRingHandle, VaultOwnerSystemId, out telemetryRing))
                {
                    return false;
                }

                locked = true;
                if (!telemetryRing.IsCreated || telemetryRing.Length < TelemetryCapacity)
                    return false;

                telemetryRing[slot] = entry;
                return true;
            }
            finally
            {
                if (locked)
                    vault.ReleaseWriteLock(in _telemetryRingHandle, VaultOwnerSystemId);
            }
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

        private void ReleaseCockpitVaultHandles(IDataVault vault)
        {
            ReleaseCockpitVaultHandle(vault, ref _buttonStatesHandle, ButtonStatesBufferId);
            ReleaseCockpitVaultHandle(vault, ref _buttonTargetsHandle, ButtonTargetsBufferId);
            ReleaseCockpitVaultHandle(vault, ref _buttonProgressHandle, ButtonProgressBufferId);
            ReleaseCockpitVaultHandle(vault, ref _buttonOffsetsHandle, ButtonOffsetsBufferId);
            ReleaseCockpitVaultHandle(vault, ref _buttonBaseLocalPositionsHandle, ButtonBaseLocalPositionsBufferId);
            ReleaseCockpitVaultHandle(vault, ref _buttonMatricesHandle, ButtonMatricesBufferId);
            ReleaseCockpitVaultHandle(vault, ref _telemetryRingHandle, TelemetryRingBufferId);
        }

        private static void ReleaseCockpitVaultHandle<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID expectedBufferId) where T : unmanaged
        {
            if (vault != null && IsExactVaultHandle(in handle, expectedBufferId))
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
            if (radarCompute == null || !_supportsComputeShadersCold)
            {
                _radarResourcesReady = false;
                _presentationResourcesDirty = false;
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
                    GraphicsBuffer.Target.IndirectArguments,
                    GraphicsBuffer.UsageFlags.LockBufferForWrite,
                    1,
                    GraphicsBuffer.IndirectDrawIndexedArgs.size); // COLD ALLOC: GraphicsBuffer[1] - radar indirect draw args A - owner: VehicleSubOsCockpitRuntime
                radarArgsBufferCreated = true;
            }

            if (_radarArgsBufferB == null)
            {
                _radarArgsBufferB = new GraphicsBuffer(
                    GraphicsBuffer.Target.IndirectArguments,
                    GraphicsBuffer.UsageFlags.LockBufferForWrite,
                    1,
                    GraphicsBuffer.IndirectDrawIndexedArgs.size); // COLD ALLOC: GraphicsBuffer[1] - radar indirect draw args B - owner: VehicleSubOsCockpitRuntime
                radarArgsBufferCreated = true;
            }

            if (_activeRadarArgsBuffer == null)
                _activeRadarArgsBuffer = _radarArgsBufferA;

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
                                   _radarThreadGroupSizeX > 0 &&
                                   radarBlipMaterial != null &&
                                   ResolveRadarMesh() != null;
            _presentationResourcesDirty = false;
        }

        private void EnsureMaterialPropertyBlocksCold()
        {
            if (_screenPropertyBlock == null)
                _screenPropertyBlock = new MaterialPropertyBlock(); // COLD ALLOC: cockpit screen per-renderer properties - owner: VehicleSubOsCockpitRuntime.
            if (_radarMaterialProperties == null)
                _radarMaterialProperties = new MaterialPropertyBlock(); // COLD ALLOC: cockpit radar per-draw properties - owner: VehicleSubOsCockpitRuntime.
            if (_damageHologramProperties == null)
                _damageHologramProperties = new MaterialPropertyBlock(); // COLD ALLOC: cockpit damage hologram per-draw properties - owner: VehicleSubOsCockpitRuntime.
        }

        private void BindAuthoredCockpitPanelCold()
        {
            GameObject authoredPanel = _authoredCockpitPanelPrefab;
            if (authoredPanel != null && authoredPanel.scene.IsValid())
            {
                Transform authoredPanelTransform = authoredPanel.transform;
                if (dashboardPanelPlane == null)
                    dashboardPanelPlane = authoredPanelTransform;
                if (centralScreenRenderer == null)
                    centralScreenRenderer = ResolveFirstRendererCold(authoredPanelTransform);
            }

            if (_sharedUiMaterial != null && centralScreenRenderer != null && centralScreenRenderer.sharedMaterial != _sharedUiMaterial)
                centralScreenRenderer.sharedMaterial = _sharedUiMaterial;
        }

        private static Renderer ResolveFirstRendererCold(Transform root)
        {
            if (root == null)
                return null;

            if (root.TryGetComponent(out Renderer renderer))
                return renderer;

            int childCount = root.childCount;
            for (int i = 0; i < childCount; i++)
            {
                Renderer childRenderer = ResolveFirstRendererCold(root.GetChild(i));
                if (childRenderer != null)
                    return childRenderer;
            }

            return null;
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
            ReleaseBuffer(ref _damagePointUploadBuffer);
            ReleaseBuffer(ref _damageArgsUploadBuffer);
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
            _damageHologramProperties?.Clear();
            _damageHologramFallbackPointUploaded = false;
            _damageHologramFallbackWarningActive = false;
            _damageHologramUsingFallbackGlyph = false;
            _lastDamageProxyMesh = null;
            _lastDamageArgsMesh = null;
            _lastDamageArgsInstanceCount = int.MinValue;
            _presentationResourcesDirty = true;
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
                    GraphicsBuffer.Target.Append | GraphicsBuffer.Target.CopyDestination,
                    MaxDamageHologramPoints,
                    16); // COLD ALLOC: GraphicsBuffer[512 float4] - GPU append hologram point cloud - owner: VehicleSubOsCockpitRuntime
                _damageHologramProperties.Clear();
                _damageHologramFallbackPointUploaded = false;
                _damageHologramFallbackWarningActive = false;
            }
            if (_damagePointUploadBuffer == null)
                _damagePointUploadBuffer = GraphicsBufferUploadUtility.CreateStructuredUploadStagingBuffer<Vector4>(MaxDamageHologramPoints); // COLD ALLOC: GraphicsBuffer[512 float4] - CPU-visible damage fallback point staging, GPU copy source only - owner: VehicleSubOsCockpitRuntime

            if (_damageArgsBuffer == null)
            {
                _damageArgsBuffer = new GraphicsBuffer(
                    GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.CopyDestination,
                    1,
                    GraphicsBuffer.IndirectDrawIndexedArgs.size); // COLD ALLOC: GraphicsBuffer[1] - damage hologram indirect args - owner: VehicleSubOsCockpitRuntime
                _damageArgsUploadBuffer = GraphicsBufferUploadUtility.CreateRawIndirectUploadStagingBuffer(
                    1,
                    GraphicsBuffer.IndirectDrawIndexedArgs.size); // COLD ALLOC: GraphicsBuffer[1] - CPU-visible damage args staging, GPU copy source only - owner: VehicleSubOsCockpitRuntime
                UpdateDamageHologramArgs(0, true);
            }
            else if (_damageArgsUploadBuffer == null)
            {
                _damageArgsUploadBuffer = GraphicsBufferUploadUtility.CreateRawIndirectUploadStagingBuffer(
                    1,
                    GraphicsBuffer.IndirectDrawIndexedArgs.size);
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

            EnsureDamageProxyVertexBuffer();

            if (damageHologramCompute != null && _damageHologramKernel < 0)
            {
                _damageHologramKernel = ResolveSupportedKernel(damageHologramCompute, DamageHologramKernelName);
                _damageHologramThreadGroupSizeX = ResolveKernelThreadGroupSizeX(
                    damageHologramCompute,
                    _damageHologramKernel);
            }

            bool fallbackReady = IsDamageHologramFallbackGlyphAllowed() &&
                                 _damagePointBuffer != null &&
                                 _damageArgsBuffer != null &&
                                 damageHologramMaterial != null &&
                                 ResolveDamagePointMesh() != null;
            bool computeReady = _damagePointBuffer != null &&
                                _damageArgsBuffer != null &&
                                damageHologramMaterial != null &&
                                _activeDamageProxyVertexBuffer != null &&
                                _activeDamageRoomWaterBuffer != null &&
                                damageHologramCompute != null &&
                                _damageHologramKernel >= 0 &&
                                IsKernelSupported(damageHologramCompute, _damageHologramKernel) &&
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

            if (sourceCount < MinDamageProxyVertices)
            {
                _damageProxyBounds = new Vector4(-0.75f, 0.75f, -0.45f, 0.35f);
                _damageProxyVertexCount = 0;
                _activeDamageProxyVertexBuffer = null;
                return;
            }

            int safeCount = math.min(sourceCount, MaxDamageHologramPoints);
            if (_damageProxyUploadVertices == null || _damageProxyUploadVertices.Length != safeCount)
                _damageProxyUploadVertices = new Vector3[safeCount]; // COLD ALLOC: Vector3[safeCount] - stable proxy vertex upload copy capped at 512 - owner: VehicleSubOsCockpitRuntime

            float minX = float.MaxValue;
            float maxX = float.MinValue;
            float minY = float.MaxValue;
            float maxY = float.MinValue;
            for (int i = 0; i < safeCount; i++)
            {
                Vector3 vertex = _damageProxySourceVertices[i];
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

            return !_radarResourcesReady;
        }

        private bool HasRadarBufferCapacity(int requiredCapacity)
        {
            return requiredCapacity > 0 &&
                   _sonarTapBufferA != null &&
                   _sonarTapBufferB != null &&
                   _radarBlipBuffer != null &&
                   _sonarTapBufferA.count >= requiredCapacity &&
                   _sonarTapBufferB.count >= requiredCapacity &&
                   _radarBlipBuffer.count >= requiredCapacity;
        }

        private bool AreRenderTargetsCurrent()
        {
            int width = ResolveUiWidth();
            int height = ResolveUiHeight();
            RenderTextureFormat format = _uiRenderTextureFormat;
            bool uiCurrent = _uiRenderTexture != null &&
                   _uiRenderTexture.width == width &&
                   _uiRenderTexture.height == height &&
                   _uiRenderTexture.format == format &&
                   (offscreenUiCamera == null || ReferenceEquals(offscreenUiCamera.targetTexture, _uiRenderTexture));
            return uiCurrent && IsExternalRenderTargetCurrent();
        }

        private bool HasPresentationResourcesReadyForFrame()
        {
            return !_graphicsResourceDisposalPending &&
                   !_presentationResourcesDirty &&
                   !_renderTargetsDirty &&
                   AreRenderTargetsCurrent();
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
            if (_externalFeedRequested)
                EnsureExternalRenderTextureCurrent();
            _renderTargetsDirty = false;
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

        private RenderTextureFormat ResolvePanelRenderTextureFormat()
        {
            return _supportsRgb565RenderTextureCold
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
            if (_externalFeedRequested)
            {
                EnsureExternalRenderTextureCurrent();
                _externalFeedActive = _externalRenderTexture != null;
                return;
            }

            ReleaseExternalRenderTexture();
            _externalFeedActive = false;
        }

        private bool IsExternalRenderTargetCurrent()
        {
            if (!_externalFeedRequested)
                return true;
            if (_externalRenderTexture == null)
                return !_externalFeedActive;

            return _externalRenderTexture.width == ResolveExternalWidth() &&
                   _externalRenderTexture.height == ResolveExternalHeight() &&
                   _externalRenderTexture.format == ResolvePanelRenderTextureFormat() &&
                   (exteriorFeedCamera == null || ReferenceEquals(exteriorFeedCamera.targetTexture, _externalRenderTexture));
        }

        private void EnsureExternalRenderTextureCurrent()
        {
            if (IsExternalRenderTargetCurrent() && _externalRenderTexture != null)
            {
                if (exteriorFeedCamera != null && !exteriorFeedCamera.enabled)
                    exteriorFeedCamera.enabled = true;
                _externalFeedActive = true;
                return;
            }

            ReleaseExternalRenderTexture();
            AcquireExternalRenderTexture();
            _externalFeedActive = _externalRenderTexture != null;
            _lastScreenTexture = null;
        }

        private void AcquireExternalRenderTexture()
        {
            if (_externalRenderTexture == null)
            {
                int width = ResolveExternalWidth();
                int height = ResolveExternalHeight();
                RenderTextureFormat format = ResolvePanelRenderTextureFormat();
                IRenderTexturePoolService pool = _cachedRenderTexturePool;
                _externalRenderTexture = pool != null
                    ? pool.Rent(width, height, format, this, 16)
                    : CreateRenderTexture(width, height, format, "VSOS_EXTCAM_RT");
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
                bool shouldEnableCamera = _externalRenderTexture != null;
                if (exteriorFeedCamera.enabled != shouldEnableCamera)
                    exteriorFeedCamera.enabled = shouldEnableCamera;
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
            _renderTargetsDirty = true;
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
            if (_externalFeedRequested && staticExternalNoiseTexture != null)
                return false;
            return true;
        }

        private int ResolveStatusDisplayMode()
        {
            if (_externalFeedActive)
                return StatusModeExternalLive;
            if (_externalFeedRequested)
                return staticExternalNoiseTexture != null ? StatusModeExternalStatic : StatusModeExternalLocked;
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
                !IsKernelSupported(radarCompute, _radarKernel) ||
                !IsRadarDrawableReady())
            {
                ClearRadarDrawState();
                return;
            }

            if (TryUploadGroundRadarPingsAndDispatchRadar())
                return;

            IPlayerCriticalSonarEchoReadModel audioRuntime = ResolvePlayerCriticalSonarEchoReadModel();
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
            _radarMaterialProperties?.Clear();
        }

        private void RenderRadarPointCloud()
        {
            if (_radarActivePoints <= 0 ||
                !_radarPowered ||
                !_radarResourcesReady ||
                radarBlipMaterial == null ||
                _radarMaterialProperties == null ||
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
                float3 origin = groundRadar.LastProbeOrigin;
                _radarMaterialProperties.Clear();
                _radarMaterialProperties.SetBuffer(HectonGroundRadarPingsId, groundRadarBuffer);
                _radarMaterialProperties.SetFloat(HectonRadarProceduralId, 1f);
                _radarMaterialProperties.SetFloat(HectonRadarGprProceduralId, 1f);
                _radarMaterialProperties.SetVector(
                    HectonRadarGprOriginRadiusId,
                    new Vector4(origin.x, origin.y, origin.z, math.max(1f, groundRadar.ScanRadiusMeters)));
            }
            else
            {
                _radarMaterialProperties.Clear();
                _radarMaterialProperties.SetBuffer(HectonRadarBlipsId, _radarBlipBuffer);
                _radarMaterialProperties.SetFloat(HectonRadarProceduralId, 1f);
                _radarMaterialProperties.SetFloat(HectonRadarGprProceduralId, 0f);
            }

            _radarMaterialProperties.SetMatrix(HectonRadarLocalToWorldId, radarLocalToWorld);

            Bounds bounds = new Bounds(anchorPosition, Vector3.one * ResolveRadarBoundsSize());
            RenderParams renderParams = new RenderParams(radarBlipMaterial)
            {
                worldBounds = bounds,
                layer = radarLayer,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                motionVectorMode = MotionVectorGenerationMode.ForceNoMotion,
                matProps = _radarMaterialProperties
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
                return;
            }

            Mesh mesh = ResolveDamagePointMesh();
            if (mesh == null || damageHologramMaterial == null || _damageHologramProperties == null || _damagePointBuffer == null || _damageArgsBuffer == null)
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
            else if (IsDamageHologramFallbackGlyphAllowed())
                UploadFallbackDamageHologramGlyph();
            else
            {
                UpdateDamageHologramArgs(0, true);
                _damageHologramEstimatedPoints = 0;
                return;
            }

            BindDamageHologramMaterial(hologramLocalToWorld);
            Vector4 anchorColumn = hologramLocalToWorld.GetColumn(3);
            Vector3 anchorPosition = new Vector3(anchorColumn.x, anchorColumn.y, anchorColumn.z);
            Bounds bounds = new Bounds(anchorPosition, Vector3.one * ResolveDamageHologramBoundsSize());
            UnityEngine.Graphics.DrawMeshInstancedIndirect(
                mesh,
                0,
                damageHologramMaterial,
                bounds,
                _damageArgsBuffer,
                0,
                _damageHologramProperties,
                ShadowCastingMode.Off,
                false,
                damageHologramLayer,
                Camera.current);
        }

        private void DispatchDamageHologramCompute()
        {
            if (damageHologramCompute == null ||
                _damageHologramKernel < 0 ||
                !IsKernelSupported(damageHologramCompute, _damageHologramKernel) ||
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
                   _supportsComputeShadersCold &&
                   _damageHologramKernel >= 0 &&
                   IsKernelSupported(damageHologramCompute, _damageHologramKernel) &&
                   _damageHologramThreadGroupSizeX > 0 &&
                   _activeDamageProxyVertexBuffer != null &&
                   _damageProxyVertexCount >= MinDamageProxyVertices &&
                   _activeDamageRoomWaterBuffer != null;
        }

        private static bool IsDamageHologramFallbackGlyphAllowed()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return true;
#else
            return false;
#endif
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
                GraphicsBufferUploadUtility.UploadArrayAndCopyWholeBuffer(
                    _damagePointUploadBuffer,
                    _damagePointBuffer,
                    _damageFallbackPoint,
                    FallbackDamageWarningPoints);
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
            if (_damageHologramProperties == null)
                return;

            _damageHologramProperties.Clear();
            _damageHologramProperties.SetBuffer(DamageHologramPointsId, _damagePointBuffer);
            _damageHologramProperties.SetBuffer(DamageRoomWaterLevelsId, _activeDamageRoomWaterBuffer);

            _damageHologramProperties.SetMatrix(DamageHologramLocalToWorldId, hologramLocalToWorld);
            Vector4 materialParams = default;
            materialParams.x = (float)SystemDispatcher.CurrentUnscaledTimeSeconds;
            materialParams.y = ResolveDamageHologramAlpha();
            materialParams.z = _damageRoomCount;
            materialParams.w = _cheapVisualWeight01;
            _damageHologramProperties.SetVector(DamageHologramParamsId, materialParams);
            _damageHologramProperties.SetVector(DamageHologramBoundsId, _damageProxyBounds);
            _damageHologramProperties.SetFloat(DamageHologramFlickerId, ResolveDamageHologramFlicker());
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

            GraphicsBuffer.IndirectDrawIndexedArgs args = default;
            args.indexCountPerInstance = mesh.GetIndexCount(0);
            args.instanceCount = (uint)safeInstanceCount;
            args.startIndex = mesh.GetIndexStart(0);
            args.baseVertexIndex = (uint)Mathf.Max(0, mesh.GetBaseVertex(0));
            args.startInstance = 0u;
            _damageHologramArgsUpload[0] = args;
            GraphicsBufferUploadUtility.UploadArrayAndCopyWholeBuffer(
                _damageArgsUploadBuffer,
                _damageArgsBuffer,
                _damageHologramArgsUpload,
                1);
            _lastDamageArgsMesh = mesh;
            _lastDamageArgsInstanceCount = safeInstanceCount;
        }

        private Mesh ResolveDamagePointMesh()
        {
            return damagePointMesh;
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
            return radarBlipMesh;
        }

        private bool IsRadarDrawableReady()
        {
            return radarBlipMaterial != null && ResolveRadarMesh() != null;
        }

        private void UpdateButtonKinematics(float deltaTime)
        {
            int safeButtonCount = ResolveButtonCount();
            if ((!_buttonAnimationActive && !_buttonUploadDirty) ||
                !TryReadButtonStates(safeButtonCount, out NativeArray<byte>.ReadOnly states) ||
                !TryReadButtonTargets(safeButtonCount, out NativeArray<byte>.ReadOnly targets) ||
                !TryReadButtonProgress(safeButtonCount, out NativeArray<float>.ReadOnly progress) ||
                !TryReadButtonBaseLocalPositions(safeButtonCount, out NativeArray<CockpitButtonBasePosition>.ReadOnly baseLocalPositions))
            {
                if (_buttonAnimationActive || _buttonUploadDirty)
                    _resourceRefreshDirty = true;
                return;
            }

            int count = math.clamp(safeButtonCount, 0, MaxButtons);
            float safeDeltaTime = math.max(0f, deltaTime);
            float travelSecondsInv = math.max(0f, ButtonTravelSecondsInv);
            float resolvedPressedLocalZ = ResolvePressedLocalZ();
            float pressedLocalZ = math.isfinite(resolvedPressedLocalZ) ? resolvedPressedLocalZ : -0.035f;
            for (int i = 0; i < count; i++)
            {
                byte state = states[i];
                float progressValue = progress[i];
                float progress01 = math.isfinite(progressValue) ? math.saturate(progressValue) : 0f;
                if (state == 1)
                {
                    float target = targets[i] == 2 ? 1f : 0f;
                    progress01 = MoveTowards(progress01, target, safeDeltaTime * travelSecondsInv);
                    if (math.abs(progress01 - target) <= 0.0001f)
                    {
                        progress01 = target;
                        state = targets[i];
                    }
                }
                else
                {
                    progress01 = state == 2 ? 1f : 0f;
                }

                _buttonStateScratch[i] = state;
                _buttonProgressScratch[i] = progress01;
                float offset = progress01 * pressedLocalZ;
                _buttonOffsetScratch[i] = offset;
                float3 position = baseLocalPositions[i].LocalPosition;
                position.z += offset;
                _buttonMatrixScratch[i] = float4x4.TRS(position, quaternion.identity, new float3(1f));
            }

            if (!TryWriteCockpitVaultBuffer(in _buttonStatesHandle, ButtonStatesBufferId, count, _buttonStateScratch) ||
                !TryWriteCockpitVaultBuffer(in _buttonProgressHandle, ButtonProgressBufferId, count, _buttonProgressScratch) ||
                !TryWriteCockpitVaultBuffer(in _buttonOffsetsHandle, ButtonOffsetsBufferId, count, _buttonOffsetScratch) ||
                !TryWriteCockpitVaultBuffer(in _buttonMatricesHandle, ButtonMatricesBufferId, count, _buttonMatrixScratch))
            {
                _resourceRefreshDirty = true;
                return;
            }

            _buttonUploadDirty = true;
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
                !TryReadButtonStates(safeButtonCount, out NativeArray<byte>.ReadOnly states) ||
                !TryReadButtonTargets(safeButtonCount, out NativeArray<byte>.ReadOnly targets))
            {
                return;
            }

            byte state = states[buttonIndex];
            byte target = targets[buttonIndex];
            byte desired = state == 2 || (state == 1 && target == 2) ? (byte)0 : (byte)2;
            if (!TryWriteButtonByteValue(in _buttonTargetsHandle, ButtonTargetsBufferId, safeButtonCount, buttonIndex, desired))
                return;
            if (!TryWriteButtonByteValue(in _buttonStatesHandle, ButtonStatesBufferId, safeButtonCount, buttonIndex, 1))
            {
                TryWriteButtonByteValue(in _buttonTargetsHandle, ButtonTargetsBufferId, safeButtonCount, buttonIndex, target);
                return;
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
            if (_externalFeedActive && _externalRenderTexture != null)
                return _externalRenderTexture;
            if (_externalFeedRequested && staticExternalNoiseTexture != null)
                return staticExternalNoiseTexture;
            return _uiRenderTexture;
        }

        private void RecordTelemetry()
        {
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
            int slot = (uint)_telemetryWriteIndex < (uint)TelemetryCapacity ? _telemetryWriteIndex : 0;
            CockpitTelemetryEntry entry = new CockpitTelemetryEntry
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

            if (!TryWriteTelemetryEntry(slot, in entry))
                return;

            bool shouldDump = false;
            bool shouldPublish = false;

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

            int entryCount = telemetryRing.IsCreated ? math.min(telemetryRing.Length, TelemetryCapacity) : 0;
            WriteDamageHolographerMirrorDump(entryCount, _telemetryWriteIndex, telemetryRing);
        }

        private void WriteDamageHolographerMirrorDump(
            int entryCount,
            int telemetryWriteIndex,
            NativeArray<CockpitTelemetryEntry>.ReadOnly telemetryRing)
        {
            if (!telemetryRing.IsCreated || entryCount <= 0)
                return;

            int count = math.min(math.min(entryCount, telemetryRing.Length), TelemetryCapacity);
            if (count <= 0)
                return;

            int byteCount = DumpHeaderBytes + count * DamageHologramDumpEntryBytes;
            NativeArray<byte> dump = NativeFaultDumpWriter.CreateTransientPayload(
                byteCount,
                nameof(VehicleSubOsCockpitRuntime),
                DamageHologramDumpPayloadLabel,
                NativeArrayOptions.UninitializedMemory);
            try
            {
                int cursor = 0;
                WriteUInt32LittleEndian(dump, ref cursor, DamageHologramTelemetryHash);
                WriteInt32LittleEndian(dump, ref cursor, count);
                WriteInt32LittleEndian(dump, ref cursor, DamageHologramDumpEntryBytes);
                WriteInt32LittleEndian(dump, ref cursor, telemetryWriteIndex);

                int start = telemetryWriteIndex - count;
                while (start < 0)
                    start += telemetryRing.Length;
                if (start >= telemetryRing.Length)
                    start %= telemetryRing.Length;

                for (int i = 0; i < count; i++)
                {
                    int slot = start + i;
                    if (slot >= telemetryRing.Length)
                        slot -= telemetryRing.Length;

                    WriteDamageHologramDumpEntry(dump, ref cursor, telemetryRing[slot]);
                }

                WriteDumpBytes(DamageHolographerMirrorDumpPath, dump, cursor);
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref dump,
                    nameof(VehicleSubOsCockpitRuntime),
                    DamageHologramDumpPayloadLabel);
            }
        }

        private static void WriteCockpitTelemetryDumpEntry(
            NativeArray<byte> dump,
            ref int cursor,
            in CockpitTelemetryEntry entry)
        {
            WriteInt32LittleEndian(dump, ref cursor, entry.Frame);
            WriteInt32LittleEndian(dump, ref cursor, entry.RadarActivePoints);
            WriteInt32LittleEndian(dump, ref cursor, entry.CockpitInteractions);
            WriteUInt32LittleEndian(dump, ref cursor, entry.Flags);
            WriteFloatLittleEndian(dump, ref cursor, entry.Power);
            WriteFloatLittleEndian(dump, ref cursor, entry.Oxygen);
            WriteFloatLittleEndian(dump, ref cursor, entry.Co2);
            WriteFloatLittleEndian(dump, ref cursor, entry.SpeedKnots);
            WriteFloatLittleEndian(dump, ref cursor, entry.AnchorPosition.x);
            WriteFloatLittleEndian(dump, ref cursor, entry.AnchorPosition.y);
            WriteFloatLittleEndian(dump, ref cursor, entry.AnchorPosition.z);
            WriteInt32LittleEndian(dump, ref cursor, entry.HoloDamagePoints);
            WriteInt32LittleEndian(dump, ref cursor, entry.HoloProxyVertices);
            WriteFloatLittleEndian(dump, ref cursor, entry.HoloFlicker);
            WriteFloatLittleEndian(dump, ref cursor, entry.HoloFlood01);
            WriteUInt32LittleEndian(dump, ref cursor, entry.HoloFlags);
        }

        private static void WriteDamageHologramDumpEntry(
            NativeArray<byte> dump,
            ref int cursor,
            in CockpitTelemetryEntry entry)
        {
            WriteInt32LittleEndian(dump, ref cursor, entry.Frame);
            WriteInt32LittleEndian(dump, ref cursor, entry.HoloDamagePoints);
            WriteInt32LittleEndian(dump, ref cursor, entry.HoloProxyVertices);
            WriteFloatLittleEndian(dump, ref cursor, entry.HoloFlicker);
            WriteFloatLittleEndian(dump, ref cursor, entry.HoloFlood01);
            WriteUInt32LittleEndian(dump, ref cursor, entry.HoloFlags);
        }

        private static void WriteInt32LittleEndian(NativeArray<byte> dump, ref int cursor, int value)
        {
            WriteUInt32LittleEndian(dump, ref cursor, unchecked((uint)value));
        }

        private static void WriteFloatLittleEndian(NativeArray<byte> dump, ref int cursor, float value)
        {
            WriteUInt32LittleEndian(dump, ref cursor, math.asuint(value));
        }

        private static void WriteUInt32LittleEndian(NativeArray<byte> dump, ref int cursor, uint value)
        {
            if (cursor + 4 > dump.Length)
                return;

            dump[cursor] = (byte)value;
            dump[cursor + 1] = (byte)(value >> 8);
            dump[cursor + 2] = (byte)(value >> 16);
            dump[cursor + 3] = (byte)(value >> 24);
            cursor += 4;
        }

        private static bool WriteDumpBytes(string path, NativeArray<byte> dump, int byteCount)
        {
            return NativeFaultDumpWriter.TryWriteAll(path, dump, byteCount);
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

        private int ResolveKernelThreadGroupSizeX(
            ComputeShader compute,
            int kernel)
        {
            if (compute == null || kernel < 0 || !_supportsComputeShadersCold)
                return 0;

            uint sizeX;
            uint sizeY;
            uint sizeZ;
            try
            {
                if (!compute.IsSupported(kernel))
                    return 0;

                compute.GetKernelThreadGroupSizes(kernel, out sizeX, out sizeY, out sizeZ);
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
            catch (UnityEngine.MissingReferenceException)
            {
                return 0;
            }
            catch (UnityEngine.UnityException)
            {
                return 0;
            }
            if (sizeX == 0u || sizeY != 1u || sizeZ != 1u || sizeX > int.MaxValue)
                return 0;

            ulong totalThreads = sizeX * (ulong)sizeY * sizeZ;
            return totalThreads <= PortableMaxComputeThreadsPerGroup ? (int)sizeX : 0;
        }

        private bool IsKernelSupported(ComputeShader compute, int kernel)
        {
            if (compute == null || kernel < 0 || !_supportsComputeShadersCold)
                return false;

            try
            {
                return compute.IsSupported(kernel);
            }
            catch (System.ObjectDisposedException)
            {
                return false;
            }
            catch (System.InvalidOperationException)
            {
                return false;
            }
            catch (System.ArgumentException)
            {
                return false;
            }
            catch (MissingReferenceException)
            {
                return false;
            }
            catch (UnityException)
            {
                return false;
            }
        }

        private int ResolveSupportedKernel(ComputeShader compute, string kernelName)
        {
            if (compute == null || !_supportsComputeShadersCold)
                return -1;

            try
            {
                if (!compute.HasKernel(kernelName))
                    return -1;

                int kernel = compute.FindKernel(kernelName);
                if (kernel < 0)
                    return -1;

                return compute.IsSupported(kernel) ? kernel : -1;
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
            if (_authoredCockpitPanelPrefab == null && dashboardPanelPlane != null)
                _authoredCockpitPanelPrefab = dashboardPanelPlane.gameObject;
            if (_sharedUiMaterial == null && centralScreenRenderer != null)
                _sharedUiMaterial = centralScreenRenderer.sharedMaterial;
            _buttonBasesInitialized = false;
        }

        private void Reset()
        {
            OnValidate();
        }

        private static float MoveTowards(float current, float target, float maxDelta)
        {
            float delta = target - current;
            if (math.abs(delta) <= maxDelta)
                return target;
            return current + math.sign(delta) * maxDelta;
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
