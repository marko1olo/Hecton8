using System;
using System.Runtime.InteropServices;
using Hecton.Localization;
using Hecton8.Bootstrap;
using Hecton8.Cartography;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Environment;
using Hecton8.Gameplay;
using Hecton8.PDA;
using Hecton8.World;
using TMPro;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Hecton8.UI
{
    /// <summary>
    /// Diegetic PDA sonar-map viewport driven by the packed cartography sector mask and acoustic threat grid.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/PDA Map Tab")]
    public sealed class PDAMapTab : MonoBehaviour, ILateFrameTickable, ISlowTickable, IPDAEventListener, IGlobalRegistryHotSwapListener
    {
        private const string SonarMapConstantsBufferName = "HectonSonarMapConstants";
        private const double DefaultSeaLevelY = WorldWaterLevelCalibrationMath.DefaultWaterLevelY;
        private const int MaxThreatPings = 8;
        private const int MaxStatusChars = 64;
        private const float AcousticOverlayRadiusMeters = 160f;
        private const uint PortableMaxComputeThreadsPerGroup = 256u;
        private const int MaxDispatchGroupsPerDimension = 65535;
        private const int MaxPredatorAupPoints = 16;
        private const int MaxHlodImpostorAupPoints = 16;
        private const int PointCloudCapacity = CartographyGridConstants.MaxVisibleMapPoints + MaxPredatorAupPoints + MaxHlodImpostorAupPoints;
        private const int SonarPointStrideBytes = 16;
        private const int SonarIndirectArgsStrideBytes = sizeof(uint) * 5;
        private const uint SonarProceduralVertexCount = 6u;
        private const float PointCloudPingBandWidth = 0.16f;
        private const int MaxMarkerVisuals = 64;
        private const int MarkerUpdateQueueCapacity = 128;
        private const int MaxMarkerUiUpdatesPerLateFrame = 10;
        private const float MarkerVisualSize = 7f;
        private static readonly int GridDimensionsId = Shader.PropertyToID("_GridDimensions");
        private static readonly int SonarPointsId = Shader.PropertyToID("_SonarPoints");
        private static readonly int SonarPointAppendBufferId = Shader.PropertyToID("_SonarPointAppendBuffer");
        private static readonly int DiscoveredSectorsId = Shader.PropertyToID("_DiscoveredSectors");
        private static readonly int IndirectArgsId = Shader.PropertyToID("_IndirectArgs");
        private static readonly int VolumeOriginId = Shader.PropertyToID("_VolumeOrigin");
        private static readonly int PlayerWorldPositionId = Shader.PropertyToID("_PlayerWorldPosition");
        private static readonly int SonarScalarParamsId = Shader.PropertyToID("_SonarScalarParams");
        private static readonly int SonarDispatchParamsId = Shader.PropertyToID("_SonarDispatchParams");
        private static readonly int SonarOverlayParamsId = Shader.PropertyToID("_SonarOverlayParams");
        private static readonly int PredatorAupBufferId = Shader.PropertyToID("_PredatorAUPBuffer");
        private static readonly int HlodAupBufferId = Shader.PropertyToID("_HlodAUPBuffer");
        private static readonly int PointCloudLocalToWorldId = Shader.PropertyToID("_PointCloudLocalToWorld");
        private static readonly int PointSizeId = Shader.PropertyToID("_PointSize");
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        private static readonly int AcousticPingSignalId = Shader.PropertyToID("_AcousticPingSignal");
        private static readonly int ActiveSonarRadiusId = Shader.PropertyToID("_ActiveSonarRadius");
        private static readonly int ActiveSonarMaxRangeId = Shader.PropertyToID("_ActiveSonarMaxRange");
        private static readonly int ActiveSonarGeoParamsId = Shader.PropertyToID("_ActiveSonarGeoParams");
        private static readonly int HeightColorizationId = Shader.PropertyToID("_HeightColorization");
        private static readonly int DepthFadeMetersId = Shader.PropertyToID("_DepthFadeMeters");
        private static readonly int CartographyVoxelR8Id = Shader.PropertyToID("_CartographyVoxelR8");
        private static readonly int CartographyGridParamsId = Shader.PropertyToID("_CartographyGridParams");
        private static readonly int CartographyVisualParamsId = Shader.PropertyToID("_CartographyVisualParams");
        private static readonly int HologramTintId = Shader.PropertyToID("_Tint");
        private static readonly int HologramGlowId = Shader.PropertyToID("_Glow");
        private static readonly int HologramQualityId = Shader.PropertyToID("_Quality");
        private static readonly uint _GhostSignalRejectedWarningHash = unchecked((uint)LocHash.Compute("PDAMapTab.GhostSignalRejected"));
        private static readonly uint _GhostSignalContextHash = unchecked((uint)LocHash.Compute("GhostSignal"));
        [StructLayout(LayoutKind.Explicit, Size = 96)]
        private struct SonarMapConstants
        {
            [FieldOffset(0)]
            public Vector4 GridDimensions;
            [FieldOffset(16)]
            public Vector4 VolumeOrigin;
            [FieldOffset(32)]
            public Vector4 PlayerWorldPosition;
            [FieldOffset(48)]
            public Vector4 ScalarParams;
            [FieldOffset(64)]
            public Vector4 DispatchParams;
            [FieldOffset(80)]
            public Vector4 OverlayParams;
        }
        private static readonly int SonarMapConstantsStrideBytes = UnsafeUtility.SizeOf<SonarMapConstants>();

        [Header("References")]
        [SerializeField, Tooltip("Authored GPU point-cloud material. Missing reference disables the PDA sonar cloud instead of creating runtime material state.")]
        private Material sonarPointCloudMaterial;
        [SerializeField, Tooltip("Compute shader that expands packed discovered sectors into the PDA point-cloud append buffer.")]
        private ComputeShader sonarMapCompute;
        [SerializeField, Tooltip("Authored holographic virtual-volume material. Missing reference disables the hologram pass instead of cloning a material.")]
        private Material hologramMapMaterial;
        [SerializeField, Tooltip("Optional explicit RawImage target. When null, the component builds its own viewport.")]
        private RawImage mapImage;
        [SerializeField, Tooltip("Optional explicit status label. When null, the component builds its own label.")]
        private TextMeshProUGUI statusLabel;
        [SerializeField, Tooltip("Optional readable font override for the map status label.")]
        private TMP_FontAsset labelFont;

        [Header("Map Styling")]
        [SerializeField, Tooltip("Tint used for holographic map edge highlights.")]
        private Color edgeTint = new Color(0.62f, 0.98f, 1f, 0.86f);
        [SerializeField, Range(0.25f, 8f), Tooltip("GPU point size for PDA sonar point-cloud primitives.")]
        private float pointCloudPointSize = 2.6f;
        [SerializeField, Range(0.05f, 1f), Tooltip("Opacity multiplier for GPU sonar point-cloud primitives.")]
        private float pointCloudOpacity = 0.82f;
        [SerializeField, Range(0.005f, 0.25f), Tooltip("World depth of the PDA point-cloud volume above the map plane.")]
        private float pointCloudDepthMeters = 0.08f;
        [SerializeField, Range(0.05f, 2f), Tooltip("Seconds between sonar-source refreshes while the PDA tab remains open.")]
        private float sourceRefreshInterval = 0.2f;

        private readonly Vector4[] _threatPings = new Vector4[MaxThreatPings]; // COLD ALLOC: Vector4[8] — PDA sonar-map threat ping upload cache — owner: PDAMapTab
        private readonly uint[] _pendingMarkerHashes = new uint[MarkerUpdateQueueCapacity]; // COLD ALLOC: uint[128] — time-sliced PDA marker update queue — owner: PDAMapTab
        private readonly RectTransform[] _markerVisualRoots = new RectTransform[MaxMarkerVisuals]; // COLD ALLOC: RectTransform[64] — prebuilt PDA map marker visual pool — owner: PDAMapTab
        private readonly CanvasGroup[] _markerVisualGroups = new CanvasGroup[MaxMarkerVisuals]; // COLD ALLOC: CanvasGroup[64] — marker visibility controls without SetActive — owner: PDAMapTab
        private readonly Image[] _markerVisualImages = new Image[MaxMarkerVisuals]; // COLD ALLOC: Image[64] — marker icon tint targets — owner: PDAMapTab
        private readonly uint[] _markerHashByVisualSlot = new uint[MaxMarkerVisuals]; // COLD ALLOC: uint[64] — marker hash to visual slot ownership — owner: PDAMapTab
        private readonly PDAMarkerSnapshot[] _markerUpdateSnapshots = new PDAMarkerSnapshot[MaxMarkerVisuals]; // COLD ALLOC: PDAMarkerSnapshot[64] — bulk marker-dirty expansion scratch — owner: PDAMapTab
        private readonly Vector4[] _emptyPredatorAupUpload = new Vector4[1]; // COLD ALLOC: Vector4[1] — zero fallback predator AUP buffer upload — owner: PDAMapTab
        private readonly Vector4[] _hlodImpostorAupUpload = new Vector4[MaxHlodImpostorAupPoints]; // COLD ALLOC: Vector4[16] - distant HLOD POI upload cache - owner: PDAMapTab
        private readonly SonarMapConstants[] _sonarMapConstantsUpload = new SonarMapConstants[1]; // COLD ALLOC: SonarMapConstants[1] — PDA compute constant-buffer upload lane — owner: PDAMapTab
        private bool _registeredLateFrame;
        private bool _registeredSlowTick;
        private bool _pdaEventsRegistered;
        private float _refreshCountdown;
        private float _animationTime;
        private int _pendingMarkerReadIndex;
        private int _pendingMarkerWriteIndex;
        private int _pendingMarkerCount;
        private int _nextMarkerVisualSlot;
        private int _activeThreatPingCount;
        private int _lastGhostSignalRejectedCycle = int.MinValue;
        private uint _observedMarkerRevision = uint.MaxValue;
        private GraphicsBuffer _pointCloudAppendBuffer;
        private GraphicsBuffer _pointCloudIndirectArgsBuffer;
        private GraphicsBuffer _sonarMapConstantsBufferA;
        private GraphicsBuffer _sonarMapConstantsBufferB;
        private GraphicsBuffer _activeSonarMapConstantsBuffer;
        private GraphicsBuffer _emptyPredatorAupBuffer;
        private GraphicsBuffer _hlodImpostorAupBufferA;
        private GraphicsBuffer _hlodImpostorAupBufferB;
        private GraphicsBuffer _activeHlodImpostorAupBuffer;
        private int _sonarMapConstantsUploadIndex;
        private int _hlodImpostorAupUploadIndex;
        private GraphicsBuffer _cartographySectorWordBuffer;
        private GraphicsBuffer _cartographyPackedR8Buffer;
        private GraphicsBuffer _cartographyPackedR8BufferA;
        private GraphicsBuffer _cartographyPackedR8BufferB;
        private bool _cartographyPackedR8WriteFlip;
        private Material _resolvedPointCloudMaterial;
        private Material _resolvedHologramMapMaterial;
        private MaterialPropertyBlock _pointCloudProperties;
        private MaterialPropertyBlock _hologramMapProperties;
        private int _sonarClearArgsKernel = -1;
        private int _sonarBuildMapPointsKernel = -1;
        private int _sonarClearArgsThreadGroupSizeX;
        private int _sonarBuildMapPointsThreadGroupSizeX;
        private int _sonarBuildMapPointsThreadGroupSizeY;
        private int _sonarBuildMapPointsThreadGroupSizeZ;
        private bool _sonarComputeKernelsResolved;
        private bool _sonarComputeKernelRepairRequested = true;
        private uint _uploadedCartographyRevision = uint.MaxValue;
        private uint _uploadedPackedCartographyRevision = uint.MaxValue;
        private int _packedUploadCountdown;
        private bool _cartographySectorBufferUploaded;
        private bool _pointCloudAssetLookupAttempted;
        private bool _missingSonarPointCloudAssetsAnnounced;
        private bool _pointCloudMapReady;
        private uint _uploadedHlodImpostorVersion = uint.MaxValue;
        private int _uploadedHlodImpostorCount = -1;
        private CharBufferPool.Lease _statusBufferLease;
        private readonly Vector3[] _mapWorldCorners = new Vector3[4]; // COLD ALLOC: Vector3[4] — PDA map point-cloud basis corners — owner: PDAMapTab
        private RectTransform _markerOverlayRoot;
        private IPdaCartographyReadModel _explorationTracker;
        private PDAMarkerRegistry _markerRegistry;
        private IEncounterDirectorService _encounterDirector;
        private IAudioService _audioService;
        private IWorldSeedProvider _worldSeedProvider;
        private IPlayerRuntimeContext _playerContext;
        private IStreamingBackpressureService _streamingBackpressureService;
        private IHectonOceanKinematicsService _oceanKinematicsService;
        private bool _hotSwapRegistered;
        private bool _coldSupportsSetConstantBuffer;
        private bool _coldSupportsComputeShaders;
        private Vector4 _cachedActiveSonarGeoParams;
        private float _cachedActiveSonarRadiusMeters;

        private void Awake()
        {
            CacheGraphicsCapabilitiesCold();
            EnsureBuilt();
        }

        private void OnEnable()
        {
            CacheGraphicsCapabilitiesCold();
            EnsureBuilt();
            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();
            TryAcquireStatusBuffer();
            TryRegisterPDAEvents();
            RegisterToTickManager();
            RefreshMapSource();
            RefreshMarkerRevisionFallback(force: true);
        }

        private void OnDisable()
        {
            UnregisterPDAEvents();
            UnregisterFromTickManager();
            TryUnregisterHotSwapListener();
            ClearPendingMarkerUpdates();
            ClearMarkerVisualSlots();
            ReleaseStatusBuffer();
            ClearCachedServices();
        }

        private void OnDestroy()
        {
            UnregisterPDAEvents();
            PDAEvents.AssertUnregistered(this, nameof(PDAMapTab));
            UnregisterFromTickManager();
            TryUnregisterHotSwapListener();
            ClearPendingMarkerUpdates();
            ClearMarkerVisualSlots();
            ReleaseStatusBuffer();
            ClearCachedServices();
            ReleaseResources();
        }

        /// <summary>
        /// Renders the GPU point-cloud cartography pass during the dispatcher LateUpdate lane.
        /// </summary>
        public void LateFrameTick()
        {
            RunVisualSync(SystemDispatcher.CurrentFrameDeltaTime);
            CachePresentationGlobalsLate();
            RenderHologramMap();
            RenderPointCloud();
            RefreshMarkerRevisionFallback(force: false);
            ProcessPendingMarkerUpdates(MaxMarkerUiUpdatesPerLateFrame);
        }

        public void SlowTick()
        {
            FlushSonarComputeKernelRepairSlow();
        }

        /// <inheritdoc />
        public void OnPDAEvent(in PDAEventPayload payload)
        {
            if ((PDAEventType)payload.EventType != PDAEventType.MarkerChanged)
                return;

            if (payload.MarkerHashID != 0u)
                EnqueueMarkerUpdate(payload.MarkerHashID);
            else
                EnqueueAllMarkerUpdates();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.PlayerExplorationRuntime:
                    _explorationTracker = currentService as IPdaCartographyReadModel;
                    break;
                case GlobalRegistryServiceSlot.PDAMarkerRuntime:
                    _markerRegistry = currentService as PDAMarkerRegistry;
                    _observedMarkerRevision = uint.MaxValue;
                    RefreshMarkerRevisionFallback(force: true);
                    break;
                case GlobalRegistryServiceSlot.EncounterDirector:
                    _encounterDirector = currentService as IEncounterDirectorService;
                    break;
                case GlobalRegistryServiceSlot.Audio:
                    CacheAudioService(currentService as IAudioService);
                    break;
                case GlobalRegistryServiceSlot.WorldSeedProvider:
                    _worldSeedProvider = currentService as IWorldSeedProvider;
                    break;
                case GlobalRegistryServiceSlot.Player:
                    _playerContext = currentService as IPlayerRuntimeContext;
                    break;
                case GlobalRegistryServiceSlot.OceanKinematics:
                    _oceanKinematicsService = currentService as IHectonOceanKinematicsService;
                    break;
                case GlobalRegistryServiceSlot.StreamingBackpressureRuntime:
                    _streamingBackpressureService = currentService as IStreamingBackpressureService;
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    UnregisterFromTickManager();
                    if (currentService != null && isActiveAndEnabled)
                        RegisterToTickManager();
                    break;
            }
        }

        internal void ConfigurePointCloudAssets(Material pointCloudMaterial, ComputeShader mapCompute, Material hologramMaterial = null)
        {
            if (pointCloudMaterial != null && !ReferenceEquals(sonarPointCloudMaterial, pointCloudMaterial))
            {
                sonarPointCloudMaterial = pointCloudMaterial;
                _pointCloudAssetLookupAttempted = false;
                _resolvedPointCloudMaterial = null;
            }

            if (hologramMaterial != null && !ReferenceEquals(hologramMapMaterial, hologramMaterial))
            {
                hologramMapMaterial = hologramMaterial;
                _pointCloudAssetLookupAttempted = false;
                _resolvedHologramMapMaterial = null;
            }

            if (mapCompute != null && !ReferenceEquals(sonarMapCompute, mapCompute))
            {
                sonarMapCompute = mapCompute;
                ResetSonarComputeKernelState();
                QueueSonarComputeKernelRepair();
                _pointCloudAssetLookupAttempted = false;
            }
        }

        private void RegisterToTickManager()
        {
            TryRegisterLateFrame();
            TryRegisterSlowTick();
        }

        private void UnregisterFromTickManager()
        {
            if (_registeredLateFrame)
            {
                SystemDispatcher.UnregisterLateFrameTickableDirect(this, PriorityLayer.UI);
                _registeredLateFrame = false;
            }

            if (_registeredSlowTick)
            {
                SystemDispatcher.Unregister((ISlowTickable)this, PriorityLayer.UI);
                _registeredSlowTick = false;
            }
        }

        private void TryRegisterLateFrame()
        {
            if (_registeredLateFrame || !Application.isPlaying)
                return;

            _registeredLateFrame = SystemDispatcher.Register((ILateFrameTickable)this, PriorityLayer.UI);
        }

        private void TryRegisterSlowTick()
        {
            if (_registeredSlowTick || !Application.isPlaying)
                return;

            _registeredSlowTick = SystemDispatcher.Register((ISlowTickable)this, PriorityLayer.UI);
        }

        private void RunVisualSync(float deltaTime)
        {
            float dt = math.max(0f, deltaTime);
            _animationTime += dt;
            _refreshCountdown -= dt;

            if (_refreshCountdown <= 0f)
            {
                _refreshCountdown = math.max(0.05f, sourceRefreshInterval);
                RefreshMapSource();
            }
        }

        private void CachePresentationGlobalsLate()
        {
            _cachedActiveSonarGeoParams = Shader.GetGlobalVector(ActiveSonarGeoParamsId);
            _cachedActiveSonarRadiusMeters = math.max(0f, Shader.GetGlobalFloat(ActiveSonarRadiusId));
        }

        private void TryRegisterPDAEvents()
        {
            if (_pdaEventsRegistered)
                return;

            _pdaEventsRegistered = PDAEvents.TryRegister(this);
        }

        private void UnregisterPDAEvents()
        {
            if (!_pdaEventsRegistered)
                return;

            PDAEvents.Unregister(this);
            _pdaEventsRegistered = false;
        }

        private void EnsureBuilt()
        {
            EnsureDrawPropertyBlocksCold();
            RectTransform root = transform as RectTransform;
            if (root == null)
                return;

            if (mapImage == null)
            {
                RectTransform mapRect = CreateRect("CartographyMap", root);
                mapRect.anchorMin = new Vector2(0f, 0.1f);
                mapRect.anchorMax = new Vector2(1f, 1f);
                mapRect.offsetMin = new Vector2(0f, 0f);
                mapRect.offsetMax = Vector2.zero;

                Image frame = mapRect.gameObject.AddComponent<Image>();
                frame.color = new Color(0.02f, 0.09f, 0.12f, 0.76f);
                frame.raycastTarget = false;

                GameObject imageOwner = new GameObject("MapImage"); // COLD ALLOC: GameObject[1] - PDA map RawImage owner - owner: PDAMapTab
                imageOwner.layer = gameObject.layer;
                RectTransform imageRect = imageOwner.AddComponent<RectTransform>(); // COLD ALLOC: RectTransform[1] - PDA map RawImage layout - owner: PDAMapTab
                imageRect.SetParent(mapRect, false);
                imageRect.anchorMin = new Vector2(0f, 0f);
                imageRect.anchorMax = new Vector2(1f, 1f);
                imageRect.offsetMin = new Vector2(10f, 10f);
                imageRect.offsetMax = new Vector2(-10f, -10f);

                mapImage = imageOwner.AddComponent<RawImage>();
                mapImage.texture = null;
                mapImage.color = Color.clear;
                mapImage.raycastTarget = false;
            }

            EnsureMarkerOverlayBuilt();

            if (statusLabel == null)
            {
                GameObject statusOwner = new GameObject("MapStatus"); // COLD ALLOC: GameObject[1] - PDA map status TMP owner - owner: PDAMapTab
                statusOwner.layer = gameObject.layer;
                RectTransform statusRect = statusOwner.AddComponent<RectTransform>(); // COLD ALLOC: RectTransform[1] - PDA map status layout - owner: PDAMapTab
                statusRect.SetParent(root, false);
                statusRect.anchorMin = new Vector2(0f, 0f);
                statusRect.anchorMax = new Vector2(1f, 0f);
                statusRect.offsetMin = new Vector2(8f, 0f);
                statusRect.offsetMax = new Vector2(-8f, 22f);

                statusLabel = statusOwner.AddComponent<TextMeshProUGUI>();
                statusLabel.font = LocalizedFontResolver.ResolveReadableFont(labelFont);
                statusLabel.fontSize = 8.5f;
                statusLabel.alignment = TextAlignmentOptions.BottomLeft;
                statusLabel.textWrappingMode = TextWrappingModes.NoWrap;
                statusLabel.raycastTarget = false;
                statusLabel.color = edgeTint;
            }

            EnsurePointCloudResources();
            if (mapImage != null)
            {
                mapImage.texture = null;
                mapImage.material = null;
                mapImage.color = Color.clear;
            }
        }

        private static RectTransform CreateRect(string name, RectTransform parent)
        {
            GameObject owner = new GameObject(name); // COLD ALLOC: GameObject[1] - PDA map child RectTransform owner - owner: PDAMapTab
            owner.layer = parent.gameObject.layer;
            RectTransform rect = owner.AddComponent<RectTransform>(); // COLD ALLOC: RectTransform[1] - PDA map child layout - owner: PDAMapTab
            rect.SetParent(parent, false);
            return rect;
        }

        private void EnsureMarkerOverlayBuilt()
        {
            if (_markerOverlayRoot != null || mapImage == null)
                return;

            RectTransform mapRect = mapImage.rectTransform;
            if (mapRect == null)
                return;

            _markerOverlayRoot = CreateRect("MarkerOverlay", mapRect);
            _markerOverlayRoot.anchorMin = Vector2.zero;
            _markerOverlayRoot.anchorMax = Vector2.one;
            _markerOverlayRoot.offsetMin = Vector2.zero;
            _markerOverlayRoot.offsetMax = Vector2.zero;

            for (int i = 0; i < MaxMarkerVisuals; i++)
            {
                RectTransform markerRoot = CreateRect("Marker", _markerOverlayRoot);
                markerRoot.anchorMin = new Vector2(0.5f, 0.5f);
                markerRoot.anchorMax = new Vector2(0.5f, 0.5f);
                markerRoot.sizeDelta = new Vector2(MarkerVisualSize, MarkerVisualSize);

                Image markerImage = markerRoot.gameObject.AddComponent<Image>();
                markerImage.raycastTarget = false;
                markerImage.color = Color.clear;

                CanvasGroup group = markerRoot.gameObject.AddComponent<CanvasGroup>();
                group.alpha = 0f;
                group.blocksRaycasts = false;
                group.interactable = false;

                _markerVisualRoots[i] = markerRoot;
                _markerVisualGroups[i] = group;
                _markerVisualImages[i] = markerImage;
            }
        }

        private void RefreshMapSource()
        {
            if (TryGetEmpBlindState(out _))
            {
                _pointCloudMapReady = false;
                _activeThreatPingCount = 0;
                WriteEmpBlindStatus();
                return;
            }

            if (!TryResolvePlayerAup(out _))
            {
                _pointCloudMapReady = false;
                _activeThreatPingCount = 0;
                WriteOfflineStatus();
                return;
            }

            IPdaCartographyReadModel explorationTracker = ResolvePdaCartographyReadModel();
            if (explorationTracker == null ||
                !explorationTracker.TryPrepareDiscoveredSectorsInfo(
                    out int axisLength,
                    out _,
                    out _,
                    out _,
                    out _))
            {
                _pointCloudMapReady = false;
                _activeThreatPingCount = 0;
                WriteOfflineStatus();
                return;
            }

            _pointCloudMapReady = true;
            RefreshThreatPings();
            WriteOnlineStatus(new Vector3Int(axisLength, 1, axisLength));
        }

        private IPdaCartographyReadModel ResolvePdaCartographyReadModel()
        {
            if (_explorationTracker != null && _explorationTracker.IsPdaCartographyReadModelActive)
                return _explorationTracker;

            return null;
        }

        private PDAMarkerRegistry ResolveMarkerRegistry()
        {
            if (_markerRegistry != null && _markerRegistry.isActiveAndEnabled)
                return _markerRegistry;

            return null;
        }

        private IEncounterDirectorService ResolveEncounterDirector()
        {
            return IsLiveUnityObjectReference(_encounterDirector) ? _encounterDirector : null;
        }

        private IAudioService ResolveAudioService()
        {
            IAudioService audioService = _audioService;
            if (IsAudioServiceUsable(audioService))
                return audioService;

            _audioService = null;
            return null;
        }

        private void CacheAudioService(IAudioService audioService)
        {
            _audioService = IsAudioServiceUsable(audioService) ? audioService : null;
        }

        private static bool IsAudioServiceUsable(IAudioService audioService)
        {
            if (audioService == null || !audioService.IsAudioRuntimeReady)
                return false;

            if (audioService is Behaviour behaviour)
                return behaviour != null && behaviour.isActiveAndEnabled;

            return true;
        }

        private IWorldSeedProvider ResolveWorldSeedProvider()
        {
            return IsLiveUnityObjectReference(_worldSeedProvider) ? _worldSeedProvider : null;
        }

        private IPlayerRuntimeContext ResolvePlayerContext()
        {
            return IsLiveUnityObjectReference(_playerContext) ? _playerContext : null;
        }

        private IStreamingBackpressureService ResolveStreamingBackpressureService()
        {
            return IsLiveUnityObjectReference(_streamingBackpressureService) ? _streamingBackpressureService : null;
        }

        private static bool IsLiveUnityObjectReference(object value)
        {
            if (value == null)
                return false;

            UnityEngine.Object unityObject = value as UnityEngine.Object;
            return ReferenceEquals(unityObject, null) || unityObject != null;
        }

        private void ClearCachedServices()
        {
            _explorationTracker = null;
            _markerRegistry = null;
            _encounterDirector = null;
            _audioService = null;
            _worldSeedProvider = null;
            _playerContext = null;
            _streamingBackpressureService = null;
            _oceanKinematicsService = null;
            _uploadedHlodImpostorVersion = uint.MaxValue;
            _uploadedHlodImpostorCount = -1;
            _observedMarkerRevision = uint.MaxValue;
        }

        private void CacheRegistryServicesCold()
        {
            _explorationTracker = GlobalRegistry.PlayerExploration as IPdaCartographyReadModel;
            _markerRegistry = GlobalRegistry.PDAMarkers;
            _encounterDirector = GlobalRegistry.EncounterDirector;
            CacheAudioService(GlobalRegistry.Audio);
            _worldSeedProvider = GlobalRegistry.WorldSeedProvider;
            _playerContext = GlobalRegistry.Player;
            _streamingBackpressureService = GlobalRegistry.StreamingBackpressure;
            _oceanKinematicsService = GlobalRegistry.OceanKinematics;
        }

        private void CacheGraphicsCapabilitiesCold()
        {
            _coldSupportsSetConstantBuffer = SystemInfo.supportsSetConstantBuffer;
            _coldSupportsComputeShaders = SystemInfo.supportsComputeShaders;
            QueueSonarComputeKernelRepair();
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

        private void EnsurePointCloudResources()
        {
            if (_pointCloudAppendBuffer == null || !_pointCloudAppendBuffer.IsValid())
            {
                _pointCloudAppendBuffer = new GraphicsBuffer(
                    GraphicsBuffer.Target.Append,
                    PointCloudCapacity,
                    SonarPointStrideBytes); // COLD ALLOC: GraphicsBuffer[32784 x 16B] — GPU-resident PDA cartography point cloud — owner: PDAMapTab
            }

            if (_pointCloudIndirectArgsBuffer == null || !_pointCloudIndirectArgsBuffer.IsValid())
            {
                _pointCloudIndirectArgsBuffer = new GraphicsBuffer(
                    GraphicsBuffer.Target.IndirectArguments,
                    1,
                    SonarIndirectArgsStrideBytes); // COLD ALLOC: GraphicsBuffer[5 uint] — GPU-written PDA sonar indirect args — owner: PDAMapTab
            }

            if (_coldSupportsSetConstantBuffer &&
                (_sonarMapConstantsBufferA == null || !_sonarMapConstantsBufferA.IsValid() ||
                 _sonarMapConstantsBufferB == null || !_sonarMapConstantsBufferB.IsValid()))
            {
                ReleaseGraphicsBuffer(ref _sonarMapConstantsBufferA);
                ReleaseGraphicsBuffer(ref _sonarMapConstantsBufferB);
                _sonarMapConstantsBufferA = new GraphicsBuffer(
                    GraphicsBuffer.Target.Constant,
                    GraphicsBuffer.UsageFlags.LockBufferForWrite,
                    1,
                    SonarMapConstantsStrideBytes); // COLD ALLOC: GraphicsBuffer[96B] — packed PDA sonar compute constants — owner: PDAMapTab
            }

            if (_coldSupportsSetConstantBuffer &&
                (_sonarMapConstantsBufferB == null || !_sonarMapConstantsBufferB.IsValid()))
            {
                _sonarMapConstantsBufferB = new GraphicsBuffer(
                    GraphicsBuffer.Target.Constant,
                    GraphicsBuffer.UsageFlags.LockBufferForWrite,
                    1,
                    SonarMapConstantsStrideBytes);
            }

            if (_activeSonarMapConstantsBuffer == null || !_activeSonarMapConstantsBuffer.IsValid())
                _activeSonarMapConstantsBuffer = _sonarMapConstantsBufferA;

            if (_emptyPredatorAupBuffer == null || !_emptyPredatorAupBuffer.IsValid())
            {
                _emptyPredatorAupBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<Vector4>(1); // COLD ALLOC: GraphicsBuffer[1 x float4] — zero fallback predator AUP buffer — owner: PDAMapTab
                GraphicsBufferUploadUtility.UploadArray(_emptyPredatorAupBuffer, _emptyPredatorAupUpload, 1);
            }

            if (_hlodImpostorAupBufferA == null || !_hlodImpostorAupBufferA.IsValid() ||
                _hlodImpostorAupBufferB == null || !_hlodImpostorAupBufferB.IsValid())
            {
                ReleaseGraphicsBuffer(ref _hlodImpostorAupBufferA);
                ReleaseGraphicsBuffer(ref _hlodImpostorAupBufferB);
                _hlodImpostorAupBufferA = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<Vector4>(MaxHlodImpostorAupPoints); // COLD ALLOC: GraphicsBuffer[16 x float4] - distant HLOD POI PDA buffer A - owner: PDAMapTab
                _hlodImpostorAupBufferB = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<Vector4>(MaxHlodImpostorAupPoints); // COLD ALLOC: GraphicsBuffer[16 x float4] - distant HLOD POI PDA buffer B - owner: PDAMapTab
                GraphicsBufferUploadUtility.UploadArray(_hlodImpostorAupBufferA, _hlodImpostorAupUpload, MaxHlodImpostorAupPoints);
                GraphicsBufferUploadUtility.UploadArray(_hlodImpostorAupBufferB, _hlodImpostorAupUpload, MaxHlodImpostorAupPoints);
                _activeHlodImpostorAupBuffer = _hlodImpostorAupBufferA;
                _hlodImpostorAupUploadIndex = 0;
            }

            if (_cartographySectorWordBuffer == null || !_cartographySectorWordBuffer.IsValid())
            {
                _cartographySectorWordBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<ulong>(
                    CartographyGridConstants.WordCount);
                _uploadedCartographyRevision = uint.MaxValue;
                _cartographySectorBufferUploaded = false;
            }

            if (_cartographyPackedR8BufferA == null || !_cartographyPackedR8BufferA.IsValid())
            {
                _cartographyPackedR8BufferA = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<uint>(
                    CartographyGridConstants.PackedUploadWordCount);
                _uploadedPackedCartographyRevision = uint.MaxValue;
                _packedUploadCountdown = 0;
            }

            if (_cartographyPackedR8BufferB == null || !_cartographyPackedR8BufferB.IsValid())
            {
                _cartographyPackedR8BufferB = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<uint>(
                    CartographyGridConstants.PackedUploadWordCount);
                _uploadedPackedCartographyRevision = uint.MaxValue;
                _packedUploadCountdown = 0;
            }

            if (_cartographyPackedR8Buffer == null || !_cartographyPackedR8Buffer.IsValid())
                _cartographyPackedR8Buffer = _cartographyPackedR8BufferA;

            TryResolvePointCloudAssets();
            if (_resolvedPointCloudMaterial == null)
                return;

            QueueSonarComputeKernelRepair();
            FlushSonarComputeKernelRepairSlow();
        }

        private bool HasSonarComputeKernelsReady()
        {
            return _sonarComputeKernelsResolved &&
                   _sonarClearArgsKernel >= 0 &&
                   _sonarBuildMapPointsKernel >= 0 &&
                   _sonarClearArgsThreadGroupSizeX > 0 &&
                   _sonarBuildMapPointsThreadGroupSizeX > 0 &&
                   _sonarBuildMapPointsThreadGroupSizeY > 0 &&
                   _sonarBuildMapPointsThreadGroupSizeZ > 0;
        }

        private void QueueSonarComputeKernelRepair()
        {
            _sonarComputeKernelRepairRequested = true;
        }

        private void FlushSonarComputeKernelRepairSlow()
        {
            if (!_sonarComputeKernelRepairRequested && HasSonarComputeKernelsReady())
                return;

            _sonarComputeKernelRepairRequested = false;
            if (!TryResolvePointCloudAssets())
            {
                _sonarComputeKernelRepairRequested = true;
                return;
            }

            if (!TryResolveSonarComputeKernels())
                _sonarComputeKernelRepairRequested = sonarMapCompute != null && _coldSupportsComputeShaders;
        }

        /// <summary>
        /// Resolves the authored sonar point-cloud material/compute pair, or reports the gap once without
        /// throwing.
        /// </summary>
        /// <remarks>
        /// This was the worst-placed assert triplet in the PDA route. <c>UnityEngine.Assertions.Assert</c> THROWS
        /// in this project - nothing under Assets sets <c>Assert.raiseExceptions = false</c> - and this method is
        /// reached from THREE places, one of which is a dispatcher tick lane:
        ///
        /// 1. <see cref="Awake"/> (:198) via <see cref="EnsureBuilt"/> (:463) and
        ///    <see cref="EnsurePointCloudResources"/> (:768).
        /// 2. <see cref="OnEnable"/> (:204) by the same route, BEFORE <c>CacheRegistryServicesCold</c>,
        ///    <c>TryRegisterHotSwapListener</c>, <c>TryAcquireStatusBuffer</c>, <c>TryRegisterPDAEvents</c>,
        ///    <c>RegisterToTickManager</c> and <c>RefreshMapSource</c> (:205-211). A throw here left the map tab
        ///    with no PDA event subscription and no late-frame or slow-tick registration for the session.
        /// 3. <see cref="SlowTick"/> (:253) via <see cref="FlushSonarComputeKernelRepairSlow"/> (:798). This is
        ///    the catastrophic path: <c>SystemDispatcher.RunSlowTick</c> has no try/catch around
        ///    <c>tickable.SlowTick()</c>, so the throw unwound the entire 10 Hz substep and silently killed
        ///    every unrelated slow-tick system downstream of this component.
        ///
        /// The <c>_pointCloudAssetLookupAttempted</c> latch did not contain that, because
        /// <see cref="ConfigurePointCloudAssets"/> clears it on every asset rebind (:314, :321, :330) and
        /// <c>ReleaseResources</c> clears it too (:2341) - each clear re-armed the throw for the next SlowTick.
        ///
        /// The asserts guarded nothing: this method returns a bool that both callers already handle. :798
        /// re-queues the repair, :769 returns early on a null resolved material, and
        /// <see cref="RenderPointCloud"/> re-checks <c>sonarMapCompute</c> at :1189. The announce latch below is
        /// deliberately separate from <c>_pointCloudAssetLookupAttempted</c> so that a rebind cannot re-arm the
        /// log on the slow-tick lane either.
        /// </remarks>
        private bool TryResolvePointCloudAssets()
        {
            if (_pointCloudAssetLookupAttempted)
                return _resolvedPointCloudMaterial != null && sonarMapCompute != null;

            _pointCloudAssetLookupAttempted = true;

            bool materialAssigned = sonarPointCloudMaterial != null;
            bool computeAssigned = sonarMapCompute != null;
            bool authoredMaterialValid = materialAssigned && sonarPointCloudMaterial.enableInstancing;

            _resolvedPointCloudMaterial = authoredMaterialValid ? sonarPointCloudMaterial : null;
            _resolvedHologramMapMaterial = hologramMapMaterial;

            bool resolved = _resolvedPointCloudMaterial != null && computeAssigned;

            // Report LAST and once per session. Every caller consumes the return value below, so a future
            // re-introduced throw here can no longer unwind the dispatcher slow-tick lane.
            if (!resolved && !_missingSonarPointCloudAssetsAnnounced)
            {
                _missingSonarPointCloudAssetsAnnounced = true;
                LogInvalidSonarPointCloudAssets(materialAssigned, authoredMaterialValid, computeAssigned);
            }

            return resolved;
        }

        /// <summary>
        /// One-shot report of an unusable authored sonar point-cloud pair. The latch guarantees single emission
        /// and every parameter is a primitive, so no string work reaches the slow-tick or late-frame cadence.
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogInvalidSonarPointCloudAssets(
            bool materialAssigned,
            bool authoredMaterialValid,
            bool computeAssigned)
        {
            if (!materialAssigned)
            {
                Hecton8.Core.H8Debug.LogError("PDAMapTab: serialized field 'sonarPointCloudMaterial' is unassigned. The PDA sonar point cloud renders nothing this session - RenderPointCloud bails on the null resolved material - but the map tab still ticks, still receives PDA marker events and still draws the hologram map and markers. Runtime material generation is forbidden: assign the authored sonar point cloud material in the inspector, or call ConfigurePointCloudAssets.");
            }
            else if (!authoredMaterialValid)
            {
                Hecton8.Core.H8Debug.LogError("PDAMapTab: the material assigned to 'sonarPointCloudMaterial' has Enable GPU Instancing OFF, which the indirect point-cloud draw requires. The PDA sonar point cloud renders nothing this session. Tick 'Enable GPU Instancing' on that material asset.");
            }

            if (!computeAssigned)
            {
                Hecton8.Core.H8Debug.LogError("PDAMapTab: serialized field 'sonarMapCompute' is unassigned, so the CSClearArgs/CSBuildMapPoints kernels can never resolve and the PDA sonar point cloud renders nothing this session. The map tab keeps ticking and the kernel repair stays queued. Assign the authored PDA sonar map compute shader in the inspector, or call ConfigurePointCloudAssets.");
            }
        }

        private bool TryResolveSonarComputeKernels()
        {
            if (_sonarComputeKernelsResolved)
                return _sonarClearArgsKernel >= 0 && _sonarBuildMapPointsKernel >= 0;

            if (sonarMapCompute == null || !_coldSupportsComputeShaders)
            {
                ResetSonarComputeKernelState();
                return false;
            }

            if (!TryFindSonarKernel(sonarMapCompute, "CSClearArgs", out _sonarClearArgsKernel) ||
                !TryFindSonarKernel(sonarMapCompute, "CSBuildMapPoints", out _sonarBuildMapPointsKernel))
            {
                ResetSonarComputeKernelState();
                return false;
            }

            if (_sonarClearArgsKernel < 0 ||
                _sonarBuildMapPointsKernel < 0 ||
                !TryValidateSonarKernelThreadGroup(sonarMapCompute, _sonarClearArgsKernel, out uint clearArgsThreadGroupSizeX, out _, out _) ||
                !TryValidateSonarKernelThreadGroup(
                    sonarMapCompute,
                    _sonarBuildMapPointsKernel,
                    out uint threadGroupSizeX,
                    out uint threadGroupSizeY,
                    out uint threadGroupSizeZ))
            {
                ResetSonarComputeKernelState();
                return false;
            }

            _sonarClearArgsThreadGroupSizeX = (int)clearArgsThreadGroupSizeX;
            _sonarBuildMapPointsThreadGroupSizeX = (int)threadGroupSizeX;
            _sonarBuildMapPointsThreadGroupSizeY = (int)threadGroupSizeY;
            _sonarBuildMapPointsThreadGroupSizeZ = (int)threadGroupSizeZ;
            _sonarComputeKernelsResolved = true;
            _sonarComputeKernelRepairRequested = false;

            return _sonarComputeKernelsResolved;
        }

        private static bool TryFindSonarKernel(ComputeShader computeShader, string kernelName, out int kernel)
        {
            kernel = -1;
            if (computeShader == null)
                return false;

            try
            {
                if (!computeShader.HasKernel(kernelName))
                    return false;

                kernel = computeShader.FindKernel(kernelName);
                return kernel >= 0;
            }
            catch (System.ObjectDisposedException)
            {
                kernel = -1;
                return false;
            }
            catch (System.InvalidOperationException)
            {
                kernel = -1;
                return false;
            }
            catch (System.ArgumentException)
            {
                kernel = -1;
                return false;
            }
            catch (MissingReferenceException)
            {
                kernel = -1;
                return false;
            }
            catch (UnityException)
            {
                kernel = -1;
                return false;
            }
        }

        private void EnsureDrawPropertyBlocksCold()
        {
            if (_pointCloudProperties == null)
                _pointCloudProperties = new MaterialPropertyBlock(); // COLD ALLOC: PDA point-cloud draw payload - owner: PDAMapTab.
            if (_hologramMapProperties == null)
                _hologramMapProperties = new MaterialPropertyBlock(); // COLD ALLOC: PDA hologram-map draw payload - owner: PDAMapTab.
        }

        private static bool TryValidateSonarKernelThreadGroup(
            ComputeShader compute,
            int kernelIndex,
            out uint sizeX,
            out uint sizeY,
            out uint sizeZ)
        {
            sizeX = 0u;
            sizeY = 0u;
            sizeZ = 0u;
            if (compute == null || kernelIndex < 0)
                return false;

            try
            {
                if (!compute.IsSupported(kernelIndex))
                    return false;

                compute.GetKernelThreadGroupSizes(kernelIndex, out sizeX, out sizeY, out sizeZ);
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
            catch (UnityEngine.MissingReferenceException)
            {
                return false;
            }
            catch (UnityEngine.UnityException)
            {
                return false;
            }
            if (sizeX == 0u || sizeY != 1u || sizeZ != 1u)
                return false;

            if (sizeX > PortableMaxComputeThreadsPerGroup)
                return false;

            return true;
        }

        private void RenderHologramMap()
        {
            if (mapImage == null || !isActiveAndEnabled || !_pointCloudMapReady)
                return;

            if (_resolvedHologramMapMaterial == null ||
                _cartographyPackedR8Buffer == null ||
                !_cartographyPackedR8Buffer.IsValid())
            {
                return;
            }

            IPdaCartographyReadModel explorationTracker = ResolvePdaCartographyReadModel();
            if (explorationTracker == null ||
                !explorationTracker.TryPrepareDiscoveredSectorsInfo(
                    out int axisLength,
                    out int originOffset,
                    out int cellSizeMeters,
                    out uint revision,
                    out _))
            {
                return;
            }

            CartographyTuningDTO tuning = explorationTracker.TryGetCartographyTuning(out CartographyTuningDTO resolvedTuning)
                ? resolvedTuning
                : CartographyVault.BuildDefaultTuning(ResolveHomeostasisQualityWeight());
            float quality = ResolveCartographyQuality(in tuning);
            int framesBetweenUploads = CartographyGridMath.ResolveUploadIntervalFrames(quality);

            if (!TryResolvePointCloudFrame(out Matrix4x4 localToWorld, out Bounds bounds, out Camera renderCamera))
                return;

            bool uploadDue = _packedUploadCountdown <= 0 || _uploadedPackedCartographyRevision != revision;
            GraphicsBuffer uploadTarget = _cartographyPackedR8WriteFlip
                ? _cartographyPackedR8BufferB
                : _cartographyPackedR8BufferA;
            if (uploadDue &&
                uploadTarget != null &&
                uploadTarget.IsValid() &&
                explorationTracker.TryUploadPreparedCartography(
                    uploadTarget,
                    quality,
                    out int resolvedCadence,
                    out uint uploadRevision))
            {
                _cartographyPackedR8Buffer = uploadTarget;
                _cartographyPackedR8WriteFlip = !_cartographyPackedR8WriteFlip;
                _uploadedPackedCartographyRevision = uploadRevision;
                framesBetweenUploads = math.max(1, resolvedCadence);
                _packedUploadCountdown = framesBetweenUploads;
            }
            else
            {
                _packedUploadCountdown = math.max(0, _packedUploadCountdown - 1);
            }

            _hologramMapProperties.Clear();
            _hologramMapProperties.SetBuffer(CartographyVoxelR8Id, _cartographyPackedR8Buffer);
            _hologramMapProperties.SetMatrix(PointCloudLocalToWorldId, localToWorld);
            _hologramMapProperties.SetColor(HologramTintId, edgeTint);
            _hologramMapProperties.SetFloat(OpacityId, pointCloudOpacity * 0.72f);
            _hologramMapProperties.SetFloat(HologramGlowId, tuning.VisualGlowIntensity);
            _hologramMapProperties.SetFloat(HologramQualityId, quality);
            _hologramMapProperties.SetVector(
                CartographyGridParamsId,
                new Vector4(axisLength, originOffset, math.max(0.0001f, quality), cellSizeMeters));
            _hologramMapProperties.SetVector(
                CartographyVisualParamsId,
                new Vector4(_animationTime, _uploadedPackedCartographyRevision, framesBetweenUploads, 0f));

            RenderParams renderParams = new RenderParams(_resolvedHologramMapMaterial)
            {
                matProps = _hologramMapProperties,
                worldBounds = bounds,
                camera = renderCamera,
                layer = gameObject.layer,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false
            };
            UnityEngine.Graphics.RenderPrimitives(renderParams, MeshTopology.Triangles, (int)SonarProceduralVertexCount, 1);
        }

        private void RenderPointCloud()
        {
            if (mapImage == null || !isActiveAndEnabled || !_pointCloudMapReady)
                return;

            if (_resolvedPointCloudMaterial == null ||
                _pointCloudAppendBuffer == null ||
                !_pointCloudAppendBuffer.IsValid() ||
                _pointCloudIndirectArgsBuffer == null ||
                !_pointCloudIndirectArgsBuffer.IsValid())
            {
                return;
            }

            if (!HasSonarComputeKernelsReady())
            {
                QueueSonarComputeKernelRepair();
                return;
            }

            if (!TryResolvePlayerAup(out AbsoluteUniversePosition playerAup))
                return;

            if (!TryResolveRuntimePosition(in playerAup, out Vector3 playerPosition))
                return;

            if (!TryResolvePointCloudFrame(out Matrix4x4 localToWorld, out Bounds bounds, out Camera renderCamera))
                return;

            IPdaCartographyReadModel explorationTracker = ResolvePdaCartographyReadModel();
            CartographyTuningDTO tuning = explorationTracker != null &&
                                          explorationTracker.TryGetCartographyTuning(out CartographyTuningDTO resolvedTuning)
                ? resolvedTuning
                : CartographyVault.BuildDefaultTuning(ResolveHomeostasisQualityWeight());
            float quality = ResolveCartographyQuality(in tuning);
            if (!DispatchSonarPointCloud(in playerAup, playerPosition, quality))
                return;

            Vector4 activeSonarGeoParams = _cachedActiveSonarGeoParams;
            float activeSonarRadiusMeters = _cachedActiveSonarRadiusMeters;
            float activeSonarMaxRangeMeters = math.max(1f, activeSonarGeoParams.y);
            float pingRadius = activeSonarGeoParams.x > 0.5f
                ? math.saturate(activeSonarRadiusMeters * math.rcp(activeSonarMaxRangeMeters))
                : math.frac(_animationTime * 0.33f) * 0.62f;
            float pingActive = activeSonarGeoParams.x > 0.5f ? 1f : 0f;
            _pointCloudProperties.Clear();
            _pointCloudProperties.SetBuffer(SonarPointsId, _pointCloudAppendBuffer);
            _pointCloudProperties.SetMatrix(PointCloudLocalToWorldId, localToWorld);
            _pointCloudProperties.SetVector(AcousticPingSignalId, new Vector4(pingRadius, PointCloudPingBandWidth, _animationTime, pingActive));
            _pointCloudProperties.SetFloat(ActiveSonarRadiusId, activeSonarRadiusMeters);
            _pointCloudProperties.SetFloat(ActiveSonarMaxRangeId, activeSonarMaxRangeMeters);
            _pointCloudProperties.SetFloat(PointSizeId, pointCloudPointSize);
            _pointCloudProperties.SetFloat(OpacityId, pointCloudOpacity);
            _pointCloudProperties.SetFloat(DepthFadeMetersId, pointCloudDepthMeters);
            _pointCloudProperties.SetFloat(HeightColorizationId, Smooth01(quality));

            UnityEngine.Graphics.DrawProceduralIndirect(
                _resolvedPointCloudMaterial,
                bounds,
                MeshTopology.Triangles,
                _pointCloudIndirectArgsBuffer,
                0,
                renderCamera,
                _pointCloudProperties,
                ShadowCastingMode.Off,
                false,
                gameObject.layer);
        }

        private bool TryResolvePointCloudFrame(out Matrix4x4 localToWorld, out Bounds bounds, out Camera renderCamera)
        {
            localToWorld = Matrix4x4.identity;
            bounds = default;
            renderCamera = null;

            RectTransform mapRect = mapImage != null ? mapImage.rectTransform : null;
            if (mapRect == null)
                return false;

            mapRect.GetWorldCorners(_mapWorldCorners);
            Vector3 bottomLeft = _mapWorldCorners[0];
            Vector3 topLeft = _mapWorldCorners[1];
            Vector3 topRight = _mapWorldCorners[2];
            Vector3 bottomRight = _mapWorldCorners[3];
            Vector3 right = bottomRight - bottomLeft;
            Vector3 up = topLeft - bottomLeft;
            Vector3 center = (bottomLeft + topLeft + topRight + bottomRight) * 0.25f;
            Vector3 normal = Vector3.Cross(right, up);
            float normalLengthSq = normal.sqrMagnitude;
            if (normalLengthSq < 0.000001f)
                return false;

            if (!TryResolvePointCloudCamera(out renderCamera) ||
                !IsPointCloudVisibleToCamera(center, renderCamera))
            {
                return false;
            }

            normal *= math.rsqrt(normalLengthSq);
            localToWorld.SetColumn(0, new Vector4(right.x, right.y, right.z, 0f));
            localToWorld.SetColumn(1, new Vector4(up.x, up.y, up.z, 0f));
            localToWorld.SetColumn(2, new Vector4(
                normal.x * pointCloudDepthMeters,
                normal.y * pointCloudDepthMeters,
                normal.z * pointCloudDepthMeters,
                0f));
            localToWorld.SetColumn(3, new Vector4(center.x, center.y, center.z, 1f));

            float boundsDepth = (pointCloudDepthMeters * 0.5f) + (math.max(pointCloudPointSize, 0.25f) * 0.004f);
            Vector3 depthOffset = normal * math.max(boundsDepth, 0.01f);
            bounds = new Bounds(bottomLeft - depthOffset, Vector3.zero);
            bounds.Encapsulate(bottomLeft + depthOffset);
            bounds.Encapsulate(topLeft - depthOffset);
            bounds.Encapsulate(topLeft + depthOffset);
            bounds.Encapsulate(topRight - depthOffset);
            bounds.Encapsulate(topRight + depthOffset);
            bounds.Encapsulate(bottomRight - depthOffset);
            bounds.Encapsulate(bottomRight + depthOffset);
            return true;
        }

        private static int ResolveDispatchGroups(int value, int divisor)
        {
            if (value <= 0 || divisor <= 0)
                return 0;

            long numerator = (long)value + divisor - 1L;
            long groups = numerator / divisor;
            if (groups <= 0L || groups > MaxDispatchGroupsPerDimension)
                return 0;

            return (int)groups;
        }

        private bool DispatchSonarPointCloud(
            in AbsoluteUniversePosition playerAup,
            Vector3 playerPosition,
            float globalQualityWeight)
        {
            if (sonarMapCompute == null ||
                _pointCloudAppendBuffer == null ||
                !_pointCloudAppendBuffer.IsValid() ||
                _pointCloudIndirectArgsBuffer == null ||
                !_pointCloudIndirectArgsBuffer.IsValid() ||
                _emptyPredatorAupBuffer == null ||
                !_emptyPredatorAupBuffer.IsValid() ||
                _activeHlodImpostorAupBuffer == null ||
                !_activeHlodImpostorAupBuffer.IsValid() ||
                _cartographySectorWordBuffer == null ||
                !_cartographySectorWordBuffer.IsValid() ||
                !_coldSupportsComputeShaders)
            {
                return false;
            }

            if (!HasSonarComputeKernelsReady())
            {
                QueueSonarComputeKernelRepair();
                return false;
            }

            IPdaCartographyReadModel explorationTracker = ResolvePdaCartographyReadModel();
            if (explorationTracker == null ||
                !explorationTracker.TryPrepareDiscoveredSectorsInfo(
                    out int axisLength,
                    out int originOffset,
                    out int cellSizeMeters,
                    out uint revision,
                    out int wordCount))
            {
                return false;
            }

            if (!_cartographySectorBufferUploaded || _uploadedCartographyRevision != revision)
            {
                if (!explorationTracker.TryUploadDiscoveredSectors(
                    _cartographySectorWordBuffer,
                    out axisLength,
                    out originOffset,
                    out cellSizeMeters,
                    out revision,
                    out wordCount))
                {
                    return false;
                }

                _uploadedCartographyRevision = revision;
                _cartographySectorBufferUploaded = true;
            }

            CartographyAup playerCartographyAup = ToCartographyAup(in playerAup);
            if (!CartographyGridMath.TryResolveMacroCell(in playerCartographyAup, out int3 playerMacroCell))
                return false;

            float qualityCurve = Smooth01(globalQualityWeight);
            int maxBitsPerWord = math.clamp((int)math.round(math.lerp(1f, 4f, qualityCurve)), 1, 4);
            int wordStride = math.clamp((int)math.round(math.lerp(8f, 1f, qualityCurve)), 1, 8);
            int dispatchWordCount = ResolveDispatchGroups(wordCount, wordStride);
            if (dispatchWordCount <= 0)
                return false;
            int clearArgsGroups = ResolveDispatchGroups(1, _sonarClearArgsThreadGroupSizeX);
            if (clearArgsGroups <= 0)
                return false;

            TryResolvePredatorAupBuffer(out GraphicsBuffer predatorAupBuffer, out int predatorAupCount);
            TryResolveHlodImpostorAupBuffer(out GraphicsBuffer hlodAupBuffer, out int hlodAupCount);
            _pointCloudAppendBuffer.SetCounterValue(0u);
            UploadSonarMapConstants(
                playerPosition,
                playerMacroCell,
                axisLength,
                originOffset,
                cellSizeMeters,
                wordCount,
                maxBitsPerWord,
                wordStride,
                qualityCurve,
                predatorAupCount,
                hlodAupCount);

            sonarMapCompute.SetBuffer(_sonarClearArgsKernel, IndirectArgsId, _pointCloudIndirectArgsBuffer);
            sonarMapCompute.Dispatch(_sonarClearArgsKernel, clearArgsGroups, 1, 1);

            sonarMapCompute.SetBuffer(_sonarBuildMapPointsKernel, DiscoveredSectorsId, _cartographySectorWordBuffer);
            sonarMapCompute.SetBuffer(_sonarBuildMapPointsKernel, SonarPointAppendBufferId, _pointCloudAppendBuffer);
            sonarMapCompute.SetBuffer(_sonarBuildMapPointsKernel, PredatorAupBufferId, predatorAupBuffer);
            sonarMapCompute.SetBuffer(_sonarBuildMapPointsKernel, HlodAupBufferId, hlodAupBuffer);
            int groupsX = ResolveDispatchGroups(dispatchWordCount, _sonarBuildMapPointsThreadGroupSizeX);
            if (groupsX <= 0)
                return false;

            sonarMapCompute.Dispatch(_sonarBuildMapPointsKernel, groupsX, 1, 1);
            GraphicsBuffer.CopyCount(_pointCloudAppendBuffer, _pointCloudIndirectArgsBuffer, sizeof(uint));
            return true;
        }

        private void UploadSonarMapConstants(
            Vector3 playerPosition,
            int3 playerMacroCell,
            int axisLength,
            int originOffset,
            int cellSizeMeters,
            int wordCount,
            int maxBitsPerWord,
            int wordStride,
            float qualityCurve,
            int predatorAupCount,
            int hlodAupCount)
        {
            SonarMapConstants constants = new SonarMapConstants
            {
                GridDimensions = new Vector4(
                    axisLength,
                    originOffset,
                    cellSizeMeters,
                    wordCount),
                VolumeOrigin = new Vector4(
                    playerMacroCell.x,
                    playerMacroCell.y,
                    playerMacroCell.z,
                    0f),
                PlayerWorldPosition = new Vector4(
                    playerPosition.x,
                    playerPosition.y,
                    playerPosition.z,
                    0f),
                ScalarParams = new Vector4(
                    AcousticOverlayRadiusMeters,
                    wordStride,
                    _animationTime,
                    PointCloudCapacity),
                DispatchParams = new Vector4(
                    wordCount,
                    maxBitsPerWord,
                    predatorAupCount,
                    SonarProceduralVertexCount),
                OverlayParams = new Vector4(
                    hlodAupCount,
                    qualityCurve,
                    0f,
                    0f)
            };

            GraphicsBuffer constantsWriteBuffer = ResolveSonarMapConstantsWriteBuffer();
            if (_coldSupportsSetConstantBuffer &&
                constantsWriteBuffer != null &&
                constantsWriteBuffer.IsValid())
            {
                _sonarMapConstantsUpload[0] = constants;
                GraphicsBufferUploadUtility.UploadArray(constantsWriteBuffer, _sonarMapConstantsUpload, 1);
                _activeSonarMapConstantsBuffer = constantsWriteBuffer;
                _sonarMapConstantsUploadIndex ^= 1;
                sonarMapCompute.SetConstantBuffer(SonarMapConstantsBufferName, _activeSonarMapConstantsBuffer, 0, SonarMapConstantsStrideBytes);
                return;
            }

            sonarMapCompute.SetVector(GridDimensionsId, constants.GridDimensions);
            sonarMapCompute.SetVector(VolumeOriginId, constants.VolumeOrigin);
            sonarMapCompute.SetVector(PlayerWorldPositionId, constants.PlayerWorldPosition);
            sonarMapCompute.SetVector(SonarScalarParamsId, constants.ScalarParams);
            sonarMapCompute.SetVector(SonarDispatchParamsId, constants.DispatchParams);
            sonarMapCompute.SetVector(SonarOverlayParamsId, constants.OverlayParams);
        }

        private GraphicsBuffer ResolveSonarMapConstantsWriteBuffer()
        {
            if (!_coldSupportsSetConstantBuffer)
                return null;

            GraphicsBuffer preferred = (_sonarMapConstantsUploadIndex & 1) == 0
                ? _sonarMapConstantsBufferB
                : _sonarMapConstantsBufferA;
            if (preferred != null && preferred.IsValid())
                return preferred;

            return _sonarMapConstantsBufferA != null && _sonarMapConstantsBufferA.IsValid()
                ? _sonarMapConstantsBufferA
                : _sonarMapConstantsBufferB;
        }

        private bool TryResolvePredatorAupBuffer(out GraphicsBuffer predatorAupBuffer, out int predatorAupCount)
        {
            predatorAupBuffer = _emptyPredatorAupBuffer;
            predatorAupCount = 0;

            IEncounterDirectorService encounterDirector = ResolveEncounterDirector();
            if (encounterDirector == null ||
                !encounterDirector.TryGetPredatorAupGpuBuffer(out GraphicsBuffer runtimeBuffer, out int runtimeCount) ||
                runtimeBuffer == null ||
                !runtimeBuffer.IsValid())
            {
                return predatorAupBuffer != null && predatorAupBuffer.IsValid();
            }

            predatorAupBuffer = runtimeBuffer;
            predatorAupCount = math.clamp(runtimeCount, 0, MaxPredatorAupPoints);
            return true;
        }

        private bool TryResolveHlodImpostorAupBuffer(out GraphicsBuffer hlodAupBuffer, out int hlodAupCount)
        {
            hlodAupBuffer = _activeHlodImpostorAupBuffer;
            hlodAupCount = 0;
            if (hlodAupBuffer == null || !hlodAupBuffer.IsValid())
                return false;

            IStreamingBackpressureService streaming = ResolveStreamingBackpressureService();
            if (streaming == null ||
                !streaming.TryGetActiveImpostorPoints(out NativeArray<StreamingHlodImpostorPoint>.ReadOnly points, out int runtimeCount) ||
                points.Length <= 0)
            {
                _uploadedHlodImpostorCount = 0;
                return true;
            }

            uint runtimeVersion = streaming.ActiveImpostorVersion;
            int uploadCount = math.clamp(math.min(runtimeCount, points.Length), 0, MaxHlodImpostorAupPoints);
            bool needsUpload = _uploadedHlodImpostorVersion != runtimeVersion ||
                               _uploadedHlodImpostorCount != uploadCount;
            if (needsUpload)
            {
                for (int i = 0; i < uploadCount; i++)
                {
                    StreamingHlodImpostorPoint point = points[i];
                    _hlodImpostorAupUpload[i] = new Vector4(
                        point.Center.x,
                        point.Center.y,
                        point.Center.z,
                        math.max(0.35f, point.Fade01));
                }

                for (int i = uploadCount; i < MaxHlodImpostorAupPoints; i++)
                    _hlodImpostorAupUpload[i] = Vector4.zero;

                GraphicsBuffer writeBuffer = ResolveHlodImpostorAupWriteBuffer();
                if (writeBuffer == null || !writeBuffer.IsValid())
                    return false;

                GraphicsBufferUploadUtility.UploadArray(writeBuffer, _hlodImpostorAupUpload, MaxHlodImpostorAupPoints);
                _activeHlodImpostorAupBuffer = writeBuffer;
                _hlodImpostorAupUploadIndex ^= 1;
                hlodAupBuffer = _activeHlodImpostorAupBuffer;
                _uploadedHlodImpostorVersion = runtimeVersion;
                _uploadedHlodImpostorCount = uploadCount;
            }

            hlodAupCount = uploadCount;
            return true;
        }

        private GraphicsBuffer ResolveHlodImpostorAupWriteBuffer()
        {
            GraphicsBuffer preferred = (_hlodImpostorAupUploadIndex & 1) == 0
                ? _hlodImpostorAupBufferB
                : _hlodImpostorAupBufferA;
            if (preferred != null && preferred.IsValid())
                return preferred;

            return _hlodImpostorAupBufferA != null && _hlodImpostorAupBufferA.IsValid()
                ? _hlodImpostorAupBufferA
                : _hlodImpostorAupBufferB;
        }

        private bool TryResolvePointCloudCamera(out Camera renderCamera)
        {
            renderCamera = GlobalRenderContext.CurrentCamera;
            if (renderCamera != null)
                return true;

            Canvas ownerCanvas = mapImage != null ? mapImage.canvas : null;
            if (ownerCanvas != null && ownerCanvas.worldCamera != null)
            {
                renderCamera = ownerCanvas.worldCamera;
                return true;
            }

            IPlayerRuntimeContext playerContext = ResolvePlayerContext();
            renderCamera = playerContext != null ? playerContext.PlayerCamera : null;
            return renderCamera != null;
        }

        private static bool IsPointCloudVisibleToCamera(Vector3 center, Camera renderCamera)
        {
            if (renderCamera == null)
                return false;

            Transform cameraTransform = renderCamera.transform;
            Vector3 toMap = center - cameraTransform.position;
            float distanceSq = toMap.sqrMagnitude;
            if (distanceSq <= 0.0001f)
                return true;

            float inverseDistance = math.rsqrt(distanceSq);
            float forwardDot = Vector3.Dot(cameraTransform.forward, toMap) * inverseDistance;
            return forwardDot > 0.025f;
        }

        private static float ResolveHomeostasisQualityWeight()
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.isfinite(quality) ? quality : 1f);
        }

        private static float ResolveCartographyQuality(in CartographyTuningDTO tuning)
        {
            float homeostasisQuality = ResolveHomeostasisQualityWeight();
            float tuningQuality = math.saturate(math.isfinite(tuning.GlobalQualityWeight) ? tuning.GlobalQualityWeight : 1f);
            return math.saturate(math.min(homeostasisQuality, tuningQuality));
        }

        private static float Smooth01(float value)
        {
            float x = math.saturate(math.isfinite(value) ? value : 1f);
            return x * x * (3f - (2f * x));
        }

        private void EnqueueMarkerUpdate(uint markerHashId)
        {
            if (markerHashId == 0u)
                return;

            for (int i = 0; i < _pendingMarkerCount; i++)
            {
                int index = _pendingMarkerReadIndex + i;
                if (index >= MarkerUpdateQueueCapacity)
                    index -= MarkerUpdateQueueCapacity;

                if (_pendingMarkerHashes[index] == markerHashId)
                    return;
            }

            if (_pendingMarkerCount >= MarkerUpdateQueueCapacity)
            {
                _pendingMarkerReadIndex++;
                if (_pendingMarkerReadIndex >= MarkerUpdateQueueCapacity)
                    _pendingMarkerReadIndex = 0;
                _pendingMarkerCount--;
            }

            _pendingMarkerHashes[_pendingMarkerWriteIndex] = markerHashId;
            _pendingMarkerWriteIndex++;
            if (_pendingMarkerWriteIndex >= MarkerUpdateQueueCapacity)
                _pendingMarkerWriteIndex = 0;

            _pendingMarkerCount++;
        }

        private void EnqueueAllMarkerUpdates()
        {
            PDAMarkerRegistry markerRegistry = ResolveMarkerRegistry();
            if (markerRegistry == null)
            {
                ClearPendingMarkerUpdates();
                ClearMarkerVisualSlots();
                return;
            }

            int markerCount = markerRegistry.CopyMarkers(_markerUpdateSnapshots, hudOnly: false);
            if (markerCount <= 0)
            {
                ClearPendingMarkerUpdates();
                ClearMarkerVisualSlots();
                return;
            }

            ClearPendingMarkerUpdates();
            ClearMarkerVisualSlotsNotInSnapshot(markerCount);
            for (int i = 0; i < markerCount; i++)
            {
                uint markerHashId = _markerUpdateSnapshots[i].MarkerHashID;
                _markerUpdateSnapshots[i] = default;
                EnqueueMarkerUpdate(markerHashId);
            }
        }

        private void RefreshMarkerRevisionFallback(bool force)
        {
            PDAMarkerRegistry markerRegistry = ResolveMarkerRegistry();
            uint revision = markerRegistry != null ? markerRegistry.Revision : 0u;
            if (!force && _observedMarkerRevision == revision)
                return;

            _observedMarkerRevision = revision;
            EnqueueAllMarkerUpdates();
        }

        private bool TryDequeueMarkerUpdate(out uint markerHashId)
        {
            markerHashId = 0u;
            if (_pendingMarkerCount <= 0)
                return false;

            markerHashId = _pendingMarkerHashes[_pendingMarkerReadIndex];
            _pendingMarkerHashes[_pendingMarkerReadIndex] = 0u;
            _pendingMarkerReadIndex++;
            if (_pendingMarkerReadIndex >= MarkerUpdateQueueCapacity)
                _pendingMarkerReadIndex = 0;

            _pendingMarkerCount--;
            return markerHashId != 0u;
        }

        private void ProcessPendingMarkerUpdates(int maxUpdates)
        {
            if (_pendingMarkerCount <= 0 || maxUpdates <= 0)
                return;

            if (_markerOverlayRoot == null)
            {
                ClearPendingMarkerUpdates();
                return;
            }

            PDAMarkerRegistry markerRegistry = ResolveMarkerRegistry();
            if (markerRegistry == null)
            {
                ClearPendingMarkerUpdates();
                return;
            }

            int processed = 0;
            while (processed < maxUpdates && TryDequeueMarkerUpdate(out uint markerHashId))
            {
                if (!markerRegistry.TryGetMarkerByHash(markerHashId, out PDAMarkerSnapshot marker))
                {
                    ClearMarkerVisualSlot(markerHashId);
                    processed++;
                    continue;
                }

                ApplyMarkerVisualization(in marker);
                processed++;
            }
        }

        private void ApplyMarkerVisualization(in PDAMarkerSnapshot marker)
        {
            if (!TryResolveMarkerVisualSlot(marker.MarkerHashID, out int slot))
                return;

            RectTransform markerRoot = _markerVisualRoots[slot];
            CanvasGroup group = _markerVisualGroups[slot];
            Image markerImage = _markerVisualImages[slot];
            if (markerRoot == null || group == null || markerImage == null || _markerOverlayRoot == null)
                return;

            float radius = math.max(1f, AcousticOverlayRadiusMeters);
            if (!TryResolveMarkerOverlayDelta(in marker, out float deltaX, out float deltaZ))
                return;

            float normalizedX = math.saturate((deltaX / (radius * 2f)) + 0.5f);
            float normalizedY = math.saturate((deltaZ / (radius * 2f)) + 0.5f);
            Rect overlayRect = _markerOverlayRoot.rect;
            markerRoot.anchoredPosition = new Vector2(
                (normalizedX - 0.5f) * overlayRect.width,
                (normalizedY - 0.5f) * overlayRect.height);

            markerImage.color = ResolveMarkerColor(marker.IconType);
            group.alpha = 1f;
            group.blocksRaycasts = false;
            group.interactable = false;
        }

        private bool TryResolveMarkerVisualSlot(uint markerHashId, out int slot)
        {
            if (markerHashId == 0u)
            {
                slot = -1;
                return false;
            }

            if (TryFindMarkerVisualSlot(markerHashId, out slot))
                return true;

            slot = _nextMarkerVisualSlot;
            _nextMarkerVisualSlot++;
            if (_nextMarkerVisualSlot >= MaxMarkerVisuals)
                _nextMarkerVisualSlot = 0;

            uint previousHash = _markerHashByVisualSlot[slot];
            _markerHashByVisualSlot[slot] = markerHashId;
            if (previousHash != 0u && previousHash != markerHashId)
                ClearMarkerVisual(slot);
            return true;
        }

        private bool TryFindMarkerVisualSlot(uint markerHashId, out int slot)
        {
            slot = -1;
            if (markerHashId == 0u)
                return false;

            for (int i = 0; i < _markerHashByVisualSlot.Length; i++)
            {
                if (_markerHashByVisualSlot[i] != markerHashId)
                    continue;

                slot = i;
                return true;
            }

            return false;
        }

        private void ClearMarkerVisualSlot(uint markerHashId)
        {
            if (!TryFindMarkerVisualSlot(markerHashId, out int slot))
                return;

            _markerHashByVisualSlot[slot] = 0u;
            ClearMarkerVisual(slot);
        }

        private void ClearMarkerVisualSlots()
        {
            for (int i = 0; i < _markerHashByVisualSlot.Length; i++)
            {
                _markerHashByVisualSlot[i] = 0u;
                ClearMarkerVisual(i);
            }

            _nextMarkerVisualSlot = 0;
        }

        private void ClearMarkerVisualSlotsNotInSnapshot(int markerCount)
        {
            for (int slot = 0; slot < _markerHashByVisualSlot.Length; slot++)
            {
                uint markerHash = _markerHashByVisualSlot[slot];
                if (markerHash == 0u || SnapshotContainsMarker(markerHash, markerCount))
                    continue;

                _markerHashByVisualSlot[slot] = 0u;
                ClearMarkerVisual(slot);
            }
        }

        private bool SnapshotContainsMarker(uint markerHashId, int markerCount)
        {
            if (markerHashId == 0u)
                return false;

            int safeCount = math.min(markerCount, _markerUpdateSnapshots.Length);
            for (int i = 0; i < safeCount; i++)
            {
                if (_markerUpdateSnapshots[i].MarkerHashID == markerHashId)
                    return true;
            }

            return false;
        }

        private void ClearMarkerVisual(int slot)
        {
            if ((uint)slot >= (uint)_markerVisualGroups.Length)
                return;

            CanvasGroup group = _markerVisualGroups[slot];
            if (group != null)
            {
                group.alpha = 0f;
                group.blocksRaycasts = false;
                group.interactable = false;
            }

            Image image = _markerVisualImages[slot];
            if (image != null)
                image.color = Color.clear;
        }

        private bool TryResolveMarkerOverlayDelta(in PDAMarkerSnapshot marker, out float deltaX, out float deltaZ)
        {
            deltaX = 0f;
            deltaZ = 0f;
            if (!TryResolvePlayerAup(out AbsoluteUniversePosition playerAup))
                return false;

            AbsoluteUniversePosition markerAup = marker.PositionAup;
            double cellSize = AbsoluteUniversePosition.CellSizeMeters;
            double x = (((double)markerAup.GridX - playerAup.GridX) * cellSize) + markerAup.LocalX - playerAup.LocalX;
            double z = (((double)markerAup.GridZ - playerAup.GridZ) * cellSize) + markerAup.LocalZ - playerAup.LocalZ;
            deltaX = (float)math.clamp(x, (double)float.MinValue, (double)float.MaxValue);
            deltaZ = (float)math.clamp(z, (double)float.MinValue, (double)float.MaxValue);
            return true;
        }

        private static Color ResolveMarkerColor(MarkerIconType iconType)
        {
            switch (iconType)
            {
                case MarkerIconType.Resource:
                    return new Color(0.32f, 0.94f, 0.58f, 0.9f);
                case MarkerIconType.Hazard:
                    return new Color(1f, 0.25f, 0.18f, 0.92f);
                case MarkerIconType.Shelter:
                    return new Color(0.35f, 0.72f, 1f, 0.9f);
                case MarkerIconType.Objective:
                    return new Color(1f, 0.85f, 0.28f, 0.94f);
                case MarkerIconType.Vehicle:
                    return new Color(0.82f, 0.62f, 1f, 0.9f);
                case MarkerIconType.Beacon:
                    return new Color(0.42f, 1f, 0.94f, 0.9f);
                default:
                    return new Color(0.7f, 0.96f, 1f, 0.86f);
            }
        }

        private void ClearPendingMarkerUpdates()
        {
            for (int i = 0; i < _pendingMarkerHashes.Length; i++)
                _pendingMarkerHashes[i] = 0u;

            _pendingMarkerReadIndex = 0;
            _pendingMarkerWriteIndex = 0;
            _pendingMarkerCount = 0;
        }

        private void RefreshThreatPings()
        {
            _activeThreatPingCount = 0;
            for (int pingIndex = 0; pingIndex < _threatPings.Length; pingIndex++)
                _threatPings[pingIndex] = Vector4.zero;

            if (WorldSpatialHashGrid.TryGetAcousticDensityMap(out NativeArray<float>.ReadOnly densityMap, out Vector3Int densityDimensions))
            {
                RefreshThreatPingsFromSpatialDensity(densityMap, densityDimensions);
                TryAppendGhostSignalPing();
                RecountThreatPings();
                return;
            }

            IAudioService audio = ResolveAudioService();
            if (audio == null)
            {
                TryAppendGhostSignalPing();
                RecountThreatPings();
                return;
            }

            NativeArray<float>.ReadOnly gridEnergy = default;
            int azimuthBins = 0;
            int elevationBins = 0;
            GraphicsBuffer radarGridBuffer = null;
            if (!audio.TryGetAcousticRadarGridPayload(
                    out gridEnergy,
                    out azimuthBins,
                    out elevationBins,
                    out radarGridBuffer))
            {
                TryAppendGhostSignalPing();
                RecountThreatPings();
                return;
            }

            int safeAzimuthBins = math.max(1, azimuthBins);
            int safeElevationBins = math.max(1, elevationBins);
            int filledPingCount = 0;
            int weakestIndex = 0;
            float weakestIntensity = 0f;
            for (int cellIndex = 0; cellIndex < gridEnergy.Length; cellIndex++)
            {
                float intensity = gridEnergy[cellIndex];
                if (intensity <= 0.025f)
                    continue;

                if (filledPingCount >= MaxThreatPings && intensity <= weakestIntensity)
                    continue;

                int azimuthIndex = cellIndex % safeAzimuthBins;
                int elevationIndex = cellIndex / safeAzimuthBins;
                Vector3 localPosition = ResolveCheapRadarGridPingPosition(
                    azimuthIndex,
                    safeAzimuthBins,
                    elevationIndex,
                    safeElevationBins);
                InsertThreatPing(localPosition, intensity, ref filledPingCount, ref weakestIndex, ref weakestIntensity);
            }

            TryAppendGhostSignalPing();
            RecountThreatPings();
        }

        private static Vector3 ResolveCheapRadarGridPingPosition(
            int azimuthIndex,
            int azimuthBins,
            int elevationIndex,
            int elevationBins)
        {
            float azimuth01 = (azimuthIndex + 0.5f) * math.rcp(math.max(1, azimuthBins));
            float phase4 = azimuth01 * 4f;
            int quadrant = math.min(3, (int)math.floor(phase4));
            float t = phase4 - quadrant;

            float x;
            float z;
            if (quadrant == 0)
            {
                x = t;
                z = 1f - t;
            }
            else if (quadrant == 1)
            {
                x = 1f - t;
                z = -t;
            }
            else if (quadrant == 2)
            {
                x = -t;
                z = -(1f - t);
            }
            else
            {
                x = -(1f - t);
                z = t;
            }

            float elevationSigned = ((elevationIndex + 0.5f) * math.rcp(math.max(1, elevationBins)) - 0.5f) * 2f;
            float y = elevationSigned * 0.70710677f;
            float horizontalScale = 1f - (math.abs(elevationSigned) * 0.29289323f);
            return new Vector3(x * horizontalScale, y, z * horizontalScale) * 0.38f;
        }

        private void RefreshThreatPingsFromSpatialDensity(NativeArray<float>.ReadOnly densityMap, Vector3Int dimensions)
        {
            if (!densityMap.IsCreated)
                return;

            int safeWidth = math.max(1, dimensions.x);
            int safeHeight = math.max(1, dimensions.y);
            int safeDepth = math.max(1, dimensions.z);
            int maxCells = math.min(densityMap.Length, safeWidth * safeHeight * safeDepth);
            int filledPingCount = 0;
            int weakestIndex = 0;
            float weakestIntensity = 0f;
            for (int cellIndex = 0; cellIndex < maxCells; cellIndex++)
            {
                float intensity = densityMap[cellIndex];
                if (intensity <= 0.025f)
                    continue;

                if (filledPingCount >= MaxThreatPings && intensity <= weakestIntensity)
                    continue;

                int z = cellIndex / (safeWidth * safeHeight);
                int y = (cellIndex - (z * safeWidth * safeHeight)) / safeWidth;
                int x = cellIndex - (z * safeWidth * safeHeight) - (y * safeWidth);
                Vector3 localPosition = new Vector3(
                    ((x + 0.5f) / safeWidth) - 0.5f,
                    ((y + 0.5f) / safeHeight) - 0.5f,
                    ((z + 0.5f) / safeDepth) - 0.5f) * 0.76f;
                InsertThreatPing(localPosition, intensity, ref filledPingCount, ref weakestIndex, ref weakestIntensity);
            }

            RecountThreatPings();
        }

        private void InsertThreatPing(
            Vector3 localPosition,
            float intensity,
            ref int filledPingCount,
            ref int weakestIndex,
            ref float weakestIntensity)
        {
            Vector4 ping = new Vector4(
                localPosition.x,
                localPosition.y,
                localPosition.z,
                math.saturate(intensity));

            if (filledPingCount < MaxThreatPings)
            {
                int insertIndex = filledPingCount;
                _threatPings[insertIndex] = ping;
                filledPingCount++;
                if (filledPingCount == MaxThreatPings)
                    ResolveWeakestThreatPing(out weakestIndex, out weakestIntensity);
                return;
            }

            if (ping.w <= weakestIntensity)
                return;

            _threatPings[weakestIndex] = ping;
            ResolveWeakestThreatPing(out weakestIndex, out weakestIntensity);
        }

        private void TryAppendGhostSignalPing()
        {
            IWorldSeedProvider worldSeedProvider = ResolveWorldSeedProvider();
            int seed = worldSeedProvider != null && worldSeedProvider.IsInitialized
                ? worldSeedProvider.RuntimeWorldSeed
                : 1;
            float unscaledTime = (float)SystemDispatcher.CurrentUnscaledTimeSeconds;
            float depthMeters = ResolvePlayerDepthMeters();
            if (!GhostSignalUtility.TryResolveCandidate(
                    seed,
                    unscaledTime,
                    depthMeters,
                    out Vector4 ghostPing))
                return;

            ResolveWeakestThreatPing(out int weakestIndex, out float weakestIntensity);

            if (weakestIndex < 0)
                return;

            if (ghostPing.w <= weakestIntensity)
            {
                TryPublishGhostSignalRejected(GhostSignalUtility.ResolveCycleIndex(unscaledTime), weakestIntensity);
                return;
            }

            _threatPings[weakestIndex] = ghostPing;
        }

        private void ResolveWeakestThreatPing(out int weakestIndex, out float weakestIntensity)
        {
            weakestIndex = -1;
            weakestIntensity = float.PositiveInfinity;
            for (int existingIndex = 0; existingIndex < MaxThreatPings; existingIndex++)
            {
                float existingIntensity = _threatPings[existingIndex].w;
                if (existingIntensity < weakestIntensity)
                {
                    weakestIntensity = existingIntensity;
                    weakestIndex = existingIndex;
                }
            }
        }

        private void RecountThreatPings()
        {
            _activeThreatPingCount = 0;
            for (int i = 0; i < MaxThreatPings; i++)
            {
                if (_threatPings[i].w > 0f)
                    _activeThreatPingCount++;
            }
        }

        private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)
        {
            IPlayerRuntimeContext playerContext = ResolvePlayerContext();
            if (playerContext != null &&
                playerContext.IsInitialized &&
                playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState) &&
                (movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u &&
                movementState.PredictedAup.IsFinite())
            {
                playerAup = movementState.PredictedAup;
                return true;
            }

            playerAup = default;
            return false;
        }

        private static bool TryResolveRuntimePosition(in AbsoluteUniversePosition targetAup, out Vector3 runtimePosition)
        {
            runtimePosition = default;
            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            double3 localDelta = AupPrecisionMath.LocalDeltaDouble(
                targetAup.ToAbsoluteDouble3(),
                originAup.ToAbsoluteDouble3());

            if (!math.all(math.isfinite(localDelta)) ||
                math.abs(localDelta.x) > AupPrecisionMath.DefaultMaxLocalCastMeters ||
                math.abs(localDelta.y) > AupPrecisionMath.DefaultMaxLocalCastMeters ||
                math.abs(localDelta.z) > AupPrecisionMath.DefaultMaxLocalCastMeters)
            {
                return false;
            }

            float3 local = default;
            local.x = (float)localDelta.x;
            local.y = (float)localDelta.y;
            local.z = (float)localDelta.z;
            if (!math.all(math.isfinite(local)))
                return false;

            runtimePosition = (Vector3)local;
            return true;
        }

        private static CartographyAup ToCartographyAup(in AbsoluteUniversePosition aup)
        {
            return new CartographyAup
            {
                GridX = aup.GridX,
                GridY = aup.GridY,
                GridZ = aup.GridZ,
                LocalX = aup.LocalX,
                LocalY = aup.LocalY,
                LocalZ = aup.LocalZ
            };
        }

        private float ResolvePlayerDepthMeters()
        {
            IPlayerRuntimeContext playerContext = ResolvePlayerContext();
            if (playerContext != null &&
                playerContext.IsInitialized &&
                playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState) &&
                (movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u &&
                math.isfinite(movementState.DepthMeters))
            {
                return math.max(0f, movementState.DepthMeters);
            }

            if (playerContext != null)
                return 0f;

            BiomeMatrixDirector biomeMatrixDirector = null;
            WorldRuntimeReferenceUtility.TryResolveBiomeMatrixDirector(ref biomeMatrixDirector);
            if (biomeMatrixDirector != null &&
                biomeMatrixDirector.isActiveAndEnabled &&
                math.isfinite(biomeMatrixDirector.CurrentDepthMeters))
            {
                return math.max(0f, biomeMatrixDirector.CurrentDepthMeters);
            }

            if (!TryResolvePlayerAup(out AbsoluteUniversePosition playerAup))
                return 0f;

            double absoluteY = playerAup.ToAbsoluteDouble3().y;
            double seaLevelY = ResolveFallbackSeaLevelY();
            return math.isfinite(absoluteY)
                ? (float)math.max(0d, seaLevelY - absoluteY)
                : 0f;
        }

        private double ResolveFallbackSeaLevelY()
        {
            IHectonOceanKinematicsService oceanKinematicsService = _oceanKinematicsService;
            IHectonOceanKinematics oceanKinematics = oceanKinematicsService != null && oceanKinematicsService.IsInitialized
                ? oceanKinematicsService.ActiveProvider
                : null;
            if (oceanKinematics != null &&
                oceanKinematics.IsAvailable &&
                TryResolveSeaLevelY(oceanKinematics.SeaLevel, out double oceanSeaLevelY))
            {
                return oceanSeaLevelY;
            }

            return DefaultSeaLevelY;
        }

        private static bool TryResolveSeaLevelY(float candidateSeaLevelY, out double seaLevelY)
        {
            if (math.isfinite(candidateSeaLevelY) &&
                math.abs(candidateSeaLevelY) <= WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY)
            {
                seaLevelY = candidateSeaLevelY;
                return true;
            }

            seaLevelY = DefaultSeaLevelY;
            return false;
        }

        private void TryPublishGhostSignalRejected(int cycleIndex, float weakestIntensity)
        {
            if (_lastGhostSignalRejectedCycle == cycleIndex)
                return;

            _lastGhostSignalRejectedCycle = cycleIndex;
            GlobalTelemetryBus.PublishPerformanceWarning(
                _GhostSignalRejectedWarningHash,
                _GhostSignalContextHash,
                weakestIntensity);
        }

        private void TryAcquireStatusBuffer()
        {
            if (_statusBufferLease.IsValid)
                return;

            CharBufferPool.TryAcquire(out _statusBufferLease);
        }

        private void ReleaseStatusBuffer()
        {
            if (!_statusBufferLease.IsValid)
                return;

            CharBufferPool.Release(_statusBufferLease);
            _statusBufferLease = default;
        }

        private void WriteOfflineStatus()
        {
            if (statusLabel == null || !_statusBufferLease.IsValid)
                return;

            char[] buffer = _statusBufferLease.Buffer;
            int length = CopyLiteral(buffer, 0, "SONAR WIRE // OFFLINE");
            statusLabel.SetCharArray(buffer, 0, length);
        }

        private void WriteEmpBlindStatus()
        {
            if (statusLabel == null || !_statusBufferLease.IsValid)
                return;

            char[] buffer = _statusBufferLease.Buffer;
            int length = CopyLiteral(buffer, 0, "SONAR WIRE // EMP BLIND");
            statusLabel.SetCharArray(buffer, 0, length);
        }

        private void WriteOnlineStatus(Vector3Int gridDimensions)
        {
            if (statusLabel == null || !_statusBufferLease.IsValid)
                return;

            Span<char> span = _statusBufferLease.Buffer.AsSpan();
            int cursor = 0;
            cursor += CopyLiteral(span, cursor, "SONAR WIRE ");
            cursor += CopyInt(span, cursor, gridDimensions.x);
            cursor += CopyLiteral(span, cursor, "x");
            cursor += CopyInt(span, cursor, gridDimensions.y);
            cursor += CopyLiteral(span, cursor, "x");
            cursor += CopyInt(span, cursor, gridDimensions.z);
            cursor += CopyLiteral(span, cursor, " // PINGS ");
            cursor += CopyInt(span, cursor, _activeThreatPingCount);
            statusLabel.SetCharArray(_statusBufferLease.Buffer, 0, cursor);
        }

        private static int CopyLiteral(char[] buffer, int offset, string literal)
        {
            if (buffer == null || string.IsNullOrEmpty(literal) || offset >= buffer.Length)
                return 0;

            int safeLength = math.min(literal.Length, buffer.Length - offset);
            literal.AsSpan(0, safeLength).CopyTo(buffer.AsSpan(offset, safeLength));
            return safeLength;
        }

        private static int CopyLiteral(Span<char> buffer, int offset, string literal)
        {
            if (string.IsNullOrEmpty(literal) || offset >= buffer.Length)
                return 0;

            int safeLength = math.min(literal.Length, buffer.Length - offset);
            literal.AsSpan(0, safeLength).CopyTo(buffer.Slice(offset, safeLength));
            return safeLength;
        }

        private static int CopyInt(Span<char> buffer, int offset, int value)
        {
            if (offset >= buffer.Length)
                return 0;

            return value.TryFormat(buffer.Slice(offset), out int written) ? written : 0;
        }

        private static Vector4 ResolveLocalVolumeHalfExtent(Vector3Int gridDimensions, Vector3 voxelCellSize)
        {
            float worldSizeX = math.max(1, gridDimensions.x - 1) * math.max(0.0001f, voxelCellSize.x);
            float worldSizeY = math.max(1, gridDimensions.y - 1) * math.max(0.0001f, voxelCellSize.y);
            float worldSizeZ = math.max(1, gridDimensions.z - 1) * math.max(0.0001f, voxelCellSize.z);
            Vector3 worldHalfExtent = new Vector3(worldSizeX, worldSizeY, worldSizeZ) * 0.5f;
            float dominantHalfExtent = math.max(0.0001f, math.max(worldHalfExtent.x, math.max(worldHalfExtent.y, worldHalfExtent.z)));
            float localScale = 0.55f / dominantHalfExtent;
            Vector3 localHalfExtent = worldHalfExtent * localScale;
            return new Vector4(localHalfExtent.x, localHalfExtent.y, localHalfExtent.z, 0f);
        }

        private bool TryGetEmpBlindState(out TraumaDispatcher traumaDispatcher)
        {
            traumaDispatcher = null;
            IPlayerRuntimeContext playerContext = ResolvePlayerContext();
            if (playerContext == null)
            {
                return false;
            }

            traumaDispatcher = playerContext.TraumaDispatcher;
            return traumaDispatcher != null && traumaDispatcher.IsEmpSensorBlindActive;
        }

        private static void ReleaseGraphicsBuffer(ref GraphicsBuffer buffer)
        {
            if (buffer == null)
                return;

            buffer.Release();
            buffer = null;
        }

        private void ReleaseResources()
        {
            if (_pointCloudAppendBuffer != null)
            {
                _pointCloudAppendBuffer.Release();
                _pointCloudAppendBuffer = null;
            }

            if (_pointCloudIndirectArgsBuffer != null)
            {
                _pointCloudIndirectArgsBuffer.Release();
                _pointCloudIndirectArgsBuffer = null;
            }

            ReleaseGraphicsBuffer(ref _sonarMapConstantsBufferA);
            ReleaseGraphicsBuffer(ref _sonarMapConstantsBufferB);
            _activeSonarMapConstantsBuffer = null;
            _sonarMapConstantsUploadIndex = 0;

            if (_emptyPredatorAupBuffer != null)
            {
                _emptyPredatorAupBuffer.Release();
                _emptyPredatorAupBuffer = null;
            }

            ReleaseGraphicsBuffer(ref _hlodImpostorAupBufferA);
            ReleaseGraphicsBuffer(ref _hlodImpostorAupBufferB);
            _activeHlodImpostorAupBuffer = null;
            _hlodImpostorAupUploadIndex = 0;

            if (_cartographySectorWordBuffer != null)
            {
                _cartographySectorWordBuffer.Release();
                _cartographySectorWordBuffer = null;
            }

            if (_cartographyPackedR8BufferA != null)
            {
                _cartographyPackedR8BufferA.Release();
                _cartographyPackedR8BufferA = null;
            }

            if (_cartographyPackedR8BufferB != null)
            {
                _cartographyPackedR8BufferB.Release();
                _cartographyPackedR8BufferB = null;
            }

            _cartographyPackedR8Buffer = null;
            _cartographyPackedR8WriteFlip = false;

            _resolvedHologramMapMaterial = null;
            _resolvedPointCloudMaterial = null;
            _pointCloudProperties?.Clear();
            _hologramMapProperties?.Clear();

            ResetSonarComputeKernelState();
            QueueSonarComputeKernelRepair();
            _uploadedCartographyRevision = uint.MaxValue;
            _uploadedPackedCartographyRevision = uint.MaxValue;
            _packedUploadCountdown = 0;
            _cartographySectorBufferUploaded = false;
            _pointCloudAssetLookupAttempted = false;
            _pointCloudMapReady = false;
        }

        private void ResetSonarComputeKernelState()
        {
            _sonarClearArgsKernel = -1;
            _sonarBuildMapPointsKernel = -1;
            _sonarClearArgsThreadGroupSizeX = 0;
            _sonarBuildMapPointsThreadGroupSizeX = 0;
            _sonarBuildMapPointsThreadGroupSizeY = 0;
            _sonarBuildMapPointsThreadGroupSizeZ = 0;
            _sonarComputeKernelsResolved = false;
        }

    }
}


