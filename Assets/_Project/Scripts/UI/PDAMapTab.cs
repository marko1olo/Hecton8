using System;
using System.Runtime.InteropServices;
using Hecton.Localization;
using Hecton8.Audio;
using Hecton8.Bootstrap;
using Hecton8.Caves;
using Hecton8.Core;
using Hecton8.Environment;
using Hecton8.Gameplay;
using Hecton8.PDA;
using Hecton8.World;
using TMPro;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Hecton8.UI
{
    /// <summary>
    /// Diegetic PDA sonar-map viewport driven by the published cave SDF snapshot and acoustic threat grid.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/PDA Map Tab")]
    public sealed class PDAMapTab : MonoBehaviour, ITickable, IUpdatable, ILateFrameTickable, IPDAEventListener
    {
        private const string SonarMapShaderPath = "Assets/_Project/Art/Shaders/Hecton_PDA_SonarMap.shader";
        private const string SonarPointCloudShaderPath = "Assets/_Project/Art/Shaders/Hecton_PDA_SonarPointCloud.shader";
        private const string SonarMapComputePath = "Assets/_Project/Art/Shaders/Hecton_SonarMap.compute";
        private const string SonarMapConstantsBufferName = "HectonSonarMapConstants";
        private const string SonarPointCloudShaderName = "Hecton8/UI/PDA Sonar Point Cloud";
        private const int MaxThreatPings = 8;
        private const int MaxStatusChars = 64;
        private static readonly bool UseHeadlessCartography = true;
        private const int CartographyTextureSize = 128;
        private const float AcousticOverlayRadiusMeters = 160f;
        private const int PointCloudThreadAxis = 8;
        private const int PointCloudLowAxis = 4;
        private const int MaxPredatorAupPoints = 16;
        private const int PointCloudCapacity = (PointCloudThreadAxis * PointCloudThreadAxis * PointCloudThreadAxis) + MaxPredatorAupPoints;
        private const int SonarPointStrideBytes = 16;
        private const int SonarIndirectArgsStrideBytes = sizeof(uint) * 5;
        private const uint SonarQuadIndexCount = 6u;
        private const float PointCloudPingBandWidth = 0.16f;
        private const int LowRaymarchSteps = 8;
        private const int HighRaymarchSteps = 16;
        private const float PointCloudTierHysteresisSeconds = 2f;
        private const int MaxMarkerVisuals = 64;
        private const int MarkerUpdateQueueCapacity = 128;
        private const int MaxMarkerUiUpdatesPerLateFrame = 10;
        private const float MarkerVisualSize = 7f;
        private static readonly int SdfVolumeId = Shader.PropertyToID("_SdfVolume");
        private static readonly int SdfRangeId = Shader.PropertyToID("_SdfRange");
        private static readonly int GridDimensionsId = Shader.PropertyToID("_GridDimensions");
        private static readonly int VolumeHalfExtentId = Shader.PropertyToID("_VolumeHalfExtent");
        private static readonly int ThreatPingCountId = Shader.PropertyToID("_ThreatPingCount");
        private static readonly int ThreatPingsId = Shader.PropertyToID("_ThreatPings");
        private static readonly int TimePhaseId = Shader.PropertyToID("_TimePhase");
        private static readonly int SonarPointsId = Shader.PropertyToID("_SonarPoints");
        private static readonly int SonarPointAppendBufferId = Shader.PropertyToID("_SonarPointAppendBuffer");
        private static readonly int VoxelSdfTexture3DId = Shader.PropertyToID("_VoxelSdfTexture3D");
        private static readonly int IndirectArgsId = Shader.PropertyToID("_IndirectArgs");
        private static readonly int VolumeOriginId = Shader.PropertyToID("_VolumeOrigin");
        private static readonly int VoxelCellSizeId = Shader.PropertyToID("_VoxelCellSize");
        private static readonly int PlayerWorldPositionId = Shader.PropertyToID("_PlayerWorldPosition");
        private static readonly int SonarScalarParamsId = Shader.PropertyToID("_SonarScalarParams");
        private static readonly int SonarDispatchParamsId = Shader.PropertyToID("_SonarDispatchParams");
        private static readonly int PredatorAupBufferId = Shader.PropertyToID("_PredatorAUPBuffer");
        private static readonly int PointCloudLocalToWorldId = Shader.PropertyToID("_PointCloudLocalToWorld");
        private static readonly int PointSizeId = Shader.PropertyToID("_PointSize");
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        private static readonly int AcousticPingSignalId = Shader.PropertyToID("_AcousticPingSignal");
        private static readonly int HeightColorizationId = Shader.PropertyToID("_HeightColorization");
        private static readonly int DepthFadeMetersId = Shader.PropertyToID("_DepthFadeMeters");
        private static readonly uint _GhostSignalRejectedWarningHash = unchecked((uint)LocHash.Compute("PDAMapTab.GhostSignalRejected"));
        private static readonly uint _GhostSignalContextHash = unchecked((uint)LocHash.Compute("GhostSignal"));
        private static readonly Vector3[] SonarQuadVertices =
        {
            new Vector3(-1f, -1f, 0f),
            new Vector3(-1f, 1f, 0f),
            new Vector3(1f, 1f, 0f),
            new Vector3(1f, -1f, 0f)
        }; // COLD ALLOC: Vector3[4] - immutable PDA sonar indirect quad vertices - owner: PDAMapTab
        private static readonly int[] SonarQuadIndices =
        {
            0, 1, 2,
            0, 2, 3
        }; // COLD ALLOC: int[6] - immutable PDA sonar indirect quad indices - owner: PDAMapTab

        [StructLayout(LayoutKind.Sequential)]
        private struct SonarMapConstants
        {
            public Vector4 GridDimensions;
            public Vector4 VolumeOrigin;
            public Vector4 VoxelCellSize;
            public Vector4 PlayerWorldPosition;
            public Vector4 ScalarParams;
            public Vector4 DispatchParams;
        }
        private static readonly int SonarMapConstantsStrideBytes = UnsafeUtility.SizeOf<SonarMapConstants>();

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct BuildCartographyTextureJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<byte> Passability;
            [ReadOnly] public NativeArray<ulong> ExplorationWords;
            [ReadOnly] public NativeArray<float> AcousticDensity;
            [WriteOnly] public NativeArray<Color32> Pixels;
            public int3 VoxelDimensions;
            public float3 VoxelOrigin;
            public float VoxelCellSize;
            public float3 PlayerPosition;
            public int TextureSize;
            public int ExplorationAxisLength;
            public int ExplorationOriginOffset;
            public int ExplorationChunkSizeMeters;
            public int3 AcousticDimensions;
            public float AcousticRadiusMeters;
            public byte SolidCell;
            public byte HasExplorationMask;
            public byte HasAcousticDensity;

            public void Execute(int index)
            {
                int width = math.max(1, TextureSize);
                int pixelX = index % width;
                int pixelY = index / width;
                float u = (pixelX + 0.5f) / width;
                float v = (pixelY + 0.5f) / width;
                int vx = math.clamp((int)math.floor(u * VoxelDimensions.x), 0, math.max(0, VoxelDimensions.x - 1));
                int vz = math.clamp((int)math.floor(v * VoxelDimensions.z), 0, math.max(0, VoxelDimensions.z - 1));
                int vy = math.clamp((int)math.floor((PlayerPosition.y - VoxelOrigin.y) / math.max(0.001f, VoxelCellSize)), 0, math.max(0, VoxelDimensions.y - 1));
                int voxelIndex = vx + (vy * VoxelDimensions.x) + (vz * VoxelDimensions.x * VoxelDimensions.y);
                float3 worldPosition = VoxelOrigin + new float3(vx * VoxelCellSize, vy * VoxelCellSize, vz * VoxelCellSize);
                bool explored = IsExplored(worldPosition.x, worldPosition.z);
                if (!explored || voxelIndex < 0 || voxelIndex >= Passability.Length)
                {
                    Pixels[index] = new Color32(0, 6, 8, 255);
                    return;
                }

                byte passability = Passability[voxelIndex];
                bool solid = passability == SolidCell;
                float acoustic = SampleAcoustic(worldPosition);
                byte r = (byte)math.clamp((solid ? 18 : 4) + (int)math.round(acoustic * 190f), 0, 255);
                byte g = (byte)math.clamp((solid ? 168 : 54) + (int)math.round(acoustic * 42f), 0, 255);
                byte b = (byte)math.clamp((solid ? 190 : 64) + (int)math.round(acoustic * 18f), 0, 255);
                Pixels[index] = new Color32(r, g, b, 255);
            }

            private bool IsExplored(float worldX, float worldZ)
            {
                if (HasExplorationMask == 0 || ExplorationWords.Length <= 0)
                    return true;

                int chunkX = (int)math.floor(worldX / math.max(1, ExplorationChunkSizeMeters));
                int chunkZ = (int)math.floor(worldZ / math.max(1, ExplorationChunkSizeMeters));
                int localX = chunkX + ExplorationOriginOffset;
                int localY = ExplorationOriginOffset;
                int localZ = chunkZ + ExplorationOriginOffset;
                if ((uint)localX >= (uint)ExplorationAxisLength ||
                    (uint)localY >= (uint)ExplorationAxisLength ||
                    (uint)localZ >= (uint)ExplorationAxisLength)
                {
                    return false;
                }

                int bitIndex = EncodeLocalMortonIndex(localX, localY, localZ);
                int wordIndex = bitIndex >> 6;
                int bit = bitIndex & 63;
                if ((uint)wordIndex >= (uint)ExplorationWords.Length)
                    return false;

                return (ExplorationWords[wordIndex] & (1UL << bit)) != 0UL;
            }

            private float SampleAcoustic(float3 worldPosition)
            {
                if (HasAcousticDensity == 0 || AcousticDensity.Length <= 0)
                    return 0f;

                float radius = math.max(0.001f, AcousticRadiusMeters);
                float3 normalized = ((worldPosition - PlayerPosition) + new float3(radius, radius, radius)) / (radius * 2f);
                if (math.any(normalized < 0f) || math.any(normalized > 1f))
                    return 0f;

                int ax = math.clamp((int)math.floor(normalized.x * AcousticDimensions.x), 0, math.max(0, AcousticDimensions.x - 1));
                int ay = math.clamp((int)math.floor(normalized.y * AcousticDimensions.y), 0, math.max(0, AcousticDimensions.y - 1));
                int az = math.clamp((int)math.floor(normalized.z * AcousticDimensions.z), 0, math.max(0, AcousticDimensions.z - 1));
                int acousticIndex = ax + (ay * AcousticDimensions.x) + (az * AcousticDimensions.x * AcousticDimensions.y);
                return (uint)acousticIndex < (uint)AcousticDensity.Length ? math.saturate(AcousticDensity[acousticIndex]) : 0f;
            }

            private int EncodeLocalMortonIndex(int x, int y, int z)
            {
                uint mask = (uint)math.max(1, ExplorationAxisLength - 1);
                uint ux = Part1By2((uint)x & mask);
                uint uy = Part1By2((uint)y & mask);
                uint uz = Part1By2((uint)z & mask);
                return (int)(ux | (uy << 1) | (uz << 2));
            }

            private static uint Part1By2(uint value)
            {
                value &= 0x0000007Fu;
                value = (value | (value << 16)) & 0x030000FFu;
                value = (value | (value << 8)) & 0x0300F00Fu;
                value = (value | (value << 4)) & 0x030C30C3u;
                value = (value | (value << 2)) & 0x09249249u;
                return value;
            }
        }

        [Header("References")]
        [SerializeField, Tooltip("Optional explicit raymarched-map shader. Editor fallback resolves the first-party asset path when left null.")]
        private Shader sonarMapShader;
        [SerializeField, Tooltip("Optional explicit GPU point-cloud shader. Editor fallback resolves the first-party asset path when left null.")]
        private Shader sonarPointCloudShader;
        [SerializeField, Tooltip("Compute shader that raymarches the published cave SDF into the PDA point-cloud append buffer.")]
        private ComputeShader sonarMapCompute;
        [SerializeField, Tooltip("Optional explicit RawImage target. When null, the component builds its own viewport.")]
        private RawImage mapImage;
        [SerializeField, Tooltip("Optional explicit status label. When null, the component builds its own label.")]
        private TextMeshProUGUI statusLabel;
        [SerializeField, Tooltip("Optional readable font override for the map status label.")]
        private TMP_FontAsset labelFont;

        [Header("Map Styling")]
        [SerializeField, Tooltip("Tint used for the holographic sonar volume.")]
        private Color mapTint = new Color(0.18f, 0.94f, 0.96f, 0.28f);
        [SerializeField, Tooltip("Tint used for edge highlights on solid SDF surfaces.")]
        private Color edgeTint = new Color(0.62f, 0.98f, 1f, 0.86f);
        [SerializeField, Tooltip("Tint used for Leviathan threat pings.")]
        private Color threatTint = new Color(1f, 0.18f, 0.14f, 0.82f);
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
        private readonly Vector4[] _emptyPredatorAupUpload = new Vector4[1]; // COLD ALLOC: Vector4[1] - zero fallback predator AUP buffer upload - owner: PDAMapTab
        private readonly SonarMapConstants[] _sonarMapConstantsUpload = new SonarMapConstants[1]; // COLD ALLOC: SonarMapConstants[1] - PDA compute constant-buffer upload lane - owner: PDAMapTab
        private bool _registered;
        private bool _registeredLateFrame;
        private bool _pdaEventsRegistered;
        private float _refreshCountdown;
        private float _animationTime;
        private int _pendingMarkerReadIndex;
        private int _pendingMarkerWriteIndex;
        private int _pendingMarkerCount;
        private int _nextMarkerVisualSlot;
        private int _activeVolumeVersion = -1;
        private int _activeThreatPingCount;
        private int _lastGhostSignalRejectedCycle = int.MinValue;
        private Texture3D _sdfTexture;
        private Texture2D _cartographyTexture;
        private NativeArray<Color32> _cartographyPixels;
        private NativeArray<ulong> _emptyExplorationWords;
        private NativeArray<float> _emptyAcousticDensity;
        private JobHandle _cartographyJobHandle;
        private JobHandle _nativeDisposeHandle;
        private bool _cartographyJobScheduled;
        private GraphicsBuffer _pointCloudAppendBuffer;
        private GraphicsBuffer _pointCloudIndirectArgsBuffer;
        private GraphicsBuffer _sonarMapConstantsBuffer;
        private GraphicsBuffer _emptyPredatorAupBuffer;
        private Material _pointCloudMaterial;
        private Mesh _pointCloudQuadMesh;
        private Material _runtimeMapMaterial;
        private HectonVoxelVolume _activeVolume;
        private Vector3Int _activeSdfGridDimensions;
        private Vector3 _activeSdfVolumeOrigin;
        private Vector3 _activeSdfVoxelCellSize = Vector3.one;
        private float _activeSdfRange = 1f;
        private int _sonarClearArgsKernel = -1;
        private int _sonarRaymarchKernel = -1;
        private int _sonarRaymarchThreadGroupSizeX = PointCloudThreadAxis;
        private int _sonarRaymarchThreadGroupSizeY = PointCloudThreadAxis;
        private int _sonarRaymarchThreadGroupSizeZ = PointCloudThreadAxis;
        private bool _sonarComputeKernelsResolved;
        private bool _pointCloudAssetLookupAttempted;
        private bool _pointCloudSdfReady;
        private bool _pointCloudTierInitialized;
        private bool _pointCloudLowTierActive;
        private bool _pointCloudLowTierCandidate;
        private float _pointCloudLowTierCandidateSince;
        private CharBufferPool.Lease _statusBufferLease;
        private readonly Vector3[] _mapWorldCorners = new Vector3[4]; // COLD ALLOC: Vector3[4] — PDA map point-cloud basis corners — owner: PDAMapTab
        private RectTransform _markerOverlayRoot;
        private int _appliedThreatPingCount = -1;
        private bool _threatPingsDirty = true;
        private void Awake()
        {
            EnsureBuilt();
        }

        private void OnEnable()
        {
            EnsureBuilt();
            _pointCloudTierInitialized = false;
            TryAcquireStatusBuffer();
            TryRegisterPDAEvents();
            RegisterToTickManager();
            RefreshMapSource(force: true);
        }

        private void OnDisable()
        {
            UnregisterPDAEvents();
            UnregisterFromTickManager();
            CompleteCartographyJobIfNeeded(applyTexture: false);
            ClearPendingMarkerUpdates();
            ClearMarkerVisualSlots();
            ReleaseStatusBuffer();
        }

        private void OnDestroy()
        {
            UnregisterPDAEvents();
            PDAEvents.AssertUnregistered(this, nameof(PDAMapTab));
            UnregisterFromTickManager();
            CompleteCartographyJobIfNeeded(applyTexture: false);
            ClearPendingMarkerUpdates();
            ClearMarkerVisualSlots();
            ReleaseStatusBuffer();
            ReleaseResources();
        }

        /// <summary>
        /// Updates the PDA sonar-map material state and refreshes the published SDF source at a bounded cadence.
        /// </summary>
        public void Tick(float deltaTime)
        {
            EnsureBuilt();
            _animationTime += deltaTime;
            _refreshCountdown -= deltaTime;

            if (_refreshCountdown <= 0f)
            {
                _refreshCountdown = math.max(0.05f, sourceRefreshInterval);
                RefreshMapSource(force: false);
            }

            PushMaterialState();
        }

        /// <summary>
        /// Applies completed headless cartography jobs during the dispatcher LateUpdate lane.
        /// </summary>
        public void LateFrameTick()
        {
            FinalizeNativeDisposeHandle();
            CompleteCartographyJobIfNeeded(applyTexture: true);
            RenderPointCloud();
            ProcessPendingMarkerUpdates(MaxMarkerUiUpdatesPerLateFrame);
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

        internal void ConfigurePointCloudAssets(Shader pointCloudShader, ComputeShader mapCompute)
        {
            if (pointCloudShader != null && !ReferenceEquals(sonarPointCloudShader, pointCloudShader))
            {
                sonarPointCloudShader = pointCloudShader;
                _pointCloudAssetLookupAttempted = false;
                if (_pointCloudMaterial != null)
                {
                    Destroy(_pointCloudMaterial);
                    _pointCloudMaterial = null;
                }
            }

            if (mapCompute == null || ReferenceEquals(sonarMapCompute, mapCompute))
                return;

            sonarMapCompute = mapCompute;
            _sonarClearArgsKernel = -1;
            _sonarRaymarchKernel = -1;
            _sonarRaymarchThreadGroupSizeX = PointCloudThreadAxis;
            _sonarRaymarchThreadGroupSizeY = PointCloudThreadAxis;
            _sonarRaymarchThreadGroupSizeZ = PointCloudThreadAxis;
            _sonarComputeKernelsResolved = false;
            _pointCloudAssetLookupAttempted = false;
        }

        private void RegisterToTickManager()
        {
            if (_registered || !Application.isPlaying)
            {
                TryRegisterLateFrame();
                return;
            }

            if (GlobalRegistry.Dispatcher == null)
                return;

            _registered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.UI);
            TryRegisterLateFrame();
        }

        private void UnregisterFromTickManager()
        {
            if (_registered)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
                _registered = false;
            }

            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.UI);
                _registeredLateFrame = false;
            }
        }

        private void TryRegisterLateFrame()
        {
            if (_registeredLateFrame || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.UI);
        }

        private void TryRegisterPDAEvents()
        {
            if (_pdaEventsRegistered)
                return;

            PDAEvents.Register(this);
            _pdaEventsRegistered = true;
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
            RectTransform root = transform as RectTransform;
            if (root == null)
                return;

            if (mapImage == null)
            {
                RectTransform mapRect = CreateRect("RaymarchedMap", root);
                mapRect.anchorMin = new Vector2(0f, 0.1f);
                mapRect.anchorMax = new Vector2(1f, 1f);
                mapRect.offsetMin = new Vector2(0f, 0f);
                mapRect.offsetMax = Vector2.zero;

                Image frame = mapRect.gameObject.AddComponent<Image>();
                frame.color = new Color(0.02f, 0.09f, 0.12f, 0.76f);
                frame.raycastTarget = false;

                GameObject imageOwner = new GameObject("MapImage", typeof(RectTransform)); // COLD ALLOC: GameObject[1] — PDA map RawImage owner — owner: PDAMapTab
                imageOwner.layer = gameObject.layer;
                imageOwner.TryGetComponent(out RectTransform imageRect);
                imageRect.SetParent(mapRect, false);
                imageRect.anchorMin = new Vector2(0f, 0f);
                imageRect.anchorMax = new Vector2(1f, 1f);
                imageRect.offsetMin = new Vector2(10f, 10f);
                imageRect.offsetMax = new Vector2(-10f, -10f);

                mapImage = imageOwner.AddComponent<RawImage>();
                mapImage.texture = Texture2D.whiteTexture;
                mapImage.color = Color.white;
                mapImage.raycastTarget = false;
            }

            EnsureMarkerOverlayBuilt();

            if (statusLabel == null)
            {
                GameObject statusOwner = new GameObject("MapStatus", typeof(RectTransform)); // COLD ALLOC: GameObject[1] — PDA map status TMP owner — owner: PDAMapTab
                statusOwner.layer = gameObject.layer;
                statusOwner.TryGetComponent(out RectTransform statusRect);
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

            if (UseHeadlessCartography)
            {
                EnsureCartographyResources();
                EnsurePointCloudResources();
                if (mapImage != null && _cartographyTexture != null)
                {
                    mapImage.texture = _cartographyTexture;
                    mapImage.material = null;
                }

                return;
            }

            if (_runtimeMapMaterial == null)
            {
#if UNITY_EDITOR
                if (sonarMapShader == null)
                    sonarMapShader = UnityEditor.AssetDatabase.LoadAssetAtPath<Shader>(SonarMapShaderPath);
#endif
                if (sonarMapShader != null)
                {
                    _runtimeMapMaterial = new Material(sonarMapShader)
                    {
                        name = "Runtime_PDASonarMap"
                    }; // COLD ALLOC: Material[1] — diegetic PDA sonar-map raymarch material — owner: PDAMapTab
                    _runtimeMapMaterial.SetColor("_MapTint", mapTint);
                    _runtimeMapMaterial.SetColor("_EdgeTint", edgeTint);
                    _runtimeMapMaterial.SetColor("_ThreatTint", threatTint);
                }
            }

            if (mapImage != null && _runtimeMapMaterial != null && mapImage.material != _runtimeMapMaterial)
                mapImage.material = _runtimeMapMaterial;
        }

        private static RectTransform CreateRect(string name, RectTransform parent)
        {
            GameObject owner = new GameObject(name, typeof(RectTransform)); // COLD ALLOC: GameObject[1] — PDA map child RectTransform owner — owner: PDAMapTab
            owner.layer = parent.gameObject.layer;
            owner.TryGetComponent(out RectTransform rect);
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

        private void RefreshMapSource(bool force)
        {
            if (TryGetEmpBlindState(out _))
            {
                _activeVolume = null;
                _activeVolumeVersion = -1;
                _pointCloudSdfReady = false;
                _activeThreatPingCount = 0;
                _threatPingsDirty = true;
                WriteEmpBlindStatus();
                return;
            }

            if (!TryResolvePlayerRuntimePosition(out Vector3 playerPosition))
            {
                _activeVolume = null;
                _activeVolumeVersion = -1;
                _pointCloudSdfReady = false;
                _activeThreatPingCount = 0;
                _threatPingsDirty = true;
                WriteOfflineStatus();
                return;
            }

            if (UseHeadlessCartography)
            {
                _pointCloudSdfReady = RefreshPointCloudSdfPayload(playerPosition, force);
                if (!ScheduleHeadlessCartography(playerPosition, force))
                {
                    if (_sdfTexture == null)
                    {
                        _activeVolume = null;
                        _activeVolumeVersion = -1;
                        _pointCloudSdfReady = false;
                    }

                    _activeThreatPingCount = 0;
                    _threatPingsDirty = true;
                    WriteOfflineStatus();
                    return;
                }

                RefreshThreatPings();
                WriteOnlineStatus(new Vector3Int(CartographyTextureSize, 1, CartographyTextureSize));
                return;
            }

            HectonVoxelEngine engine = HectonVoxelEngine.ActiveRuntimeInstance;
            if (engine == null || !engine.TryGetNearestActiveVolume(playerPosition, out HectonVoxelVolume volume))
            {
                _activeVolume = null;
                _activeVolumeVersion = -1;
                _pointCloudSdfReady = false;
                _activeThreatPingCount = 0;
                _threatPingsDirty = true;
                WriteOfflineStatus();
                return;
            }

            if (!volume.TryGetPublishedSonarSdfPayload(
                    out NativeArray<byte> encodedSdf,
                    out Vector3Int gridDimensions,
                    out Vector3 volumeOrigin,
                    out Vector3 voxelCellSize,
                    out float sdfRange,
                    out int version))
            {
                _activeVolume = volume;
                _activeVolumeVersion = -1;
                _pointCloudSdfReady = false;
                _activeThreatPingCount = 0;
                _threatPingsDirty = true;
                WriteOfflineStatus();
                return;
            }

            bool sourceChanged = force ||
                                 !ReferenceEquals(_activeVolume, volume) ||
                                 _activeVolumeVersion != version;
            _activeVolume = volume;
            _activeSdfGridDimensions = gridDimensions;
            _activeSdfVolumeOrigin = volumeOrigin;
            _activeSdfVoxelCellSize = new Vector3(
                math.max(0.0001f, voxelCellSize.x),
                math.max(0.0001f, voxelCellSize.y),
                math.max(0.0001f, voxelCellSize.z));
            _activeSdfRange = math.max(0.001f, sdfRange);
            _pointCloudSdfReady = true;
            if (sourceChanged)
            {
                EnsureSdfTexture(gridDimensions);
                _sdfTexture.SetPixelData(encodedSdf, 0);
                _sdfTexture.Apply(false, false);
                _activeVolumeVersion = version;

                if (_runtimeMapMaterial != null)
                {
                    _runtimeMapMaterial.SetTexture(SdfVolumeId, _sdfTexture);
                    _runtimeMapMaterial.SetFloat(SdfRangeId, sdfRange);
                    _runtimeMapMaterial.SetVector(
                        GridDimensionsId,
                        new Vector4(gridDimensions.x, gridDimensions.y, gridDimensions.z, 0f));
                    _runtimeMapMaterial.SetVector(VolumeHalfExtentId, ResolveLocalVolumeHalfExtent(gridDimensions, voxelCellSize));
                }
            }

            RefreshThreatPings();
            WriteOnlineStatus(gridDimensions);
        }

        private bool RefreshPointCloudSdfPayload(Vector3 playerPosition, bool force)
        {
            HectonVoxelEngine engine = HectonVoxelEngine.ActiveRuntimeInstance;
            if (engine == null || !engine.TryGetNearestActiveVolume(playerPosition, out HectonVoxelVolume volume))
            {
                _pointCloudSdfReady = false;
                return false;
            }

            if (!volume.TryGetPublishedSonarSdfPayload(
                    out NativeArray<byte> encodedSdf,
                    out Vector3Int gridDimensions,
                    out Vector3 volumeOrigin,
                    out Vector3 voxelCellSize,
                    out float sdfRange,
                    out int version))
            {
                _pointCloudSdfReady = false;
                return false;
            }

            bool sourceChanged = force ||
                                 _sdfTexture == null ||
                                 !ReferenceEquals(_activeVolume, volume) ||
                                 _activeVolumeVersion != version ||
                                 _activeSdfGridDimensions != gridDimensions;
            _activeVolume = volume;
            _activeSdfGridDimensions = gridDimensions;
            _activeSdfVolumeOrigin = volumeOrigin;
            _activeSdfVoxelCellSize = new Vector3(
                math.max(0.0001f, voxelCellSize.x),
                math.max(0.0001f, voxelCellSize.y),
                math.max(0.0001f, voxelCellSize.z));
            _activeSdfRange = math.max(0.001f, sdfRange);

            if (!sourceChanged)
                return true;

            EnsureSdfTexture(gridDimensions);
            if (_sdfTexture == null)
                return false;

            _sdfTexture.SetPixelData(encodedSdf, 0);
            _sdfTexture.Apply(false, false);
            _activeVolumeVersion = version;
            _pointCloudSdfReady = true;
            return true;
        }

        private bool ScheduleHeadlessCartography(Vector3 playerPosition, bool force)
        {
            if (_cartographyJobScheduled)
                return true;

            EnsureCartographyResources();
            if (!_cartographyPixels.IsCreated)
                return false;

            if (!VoxelDynamicNavGridRuntime.TryGetNearestPassabilityPayload(
                    (float3)playerPosition,
                    out NativeArray<byte> passability,
                    out int3 voxelDimensions,
                    out float3 voxelOrigin,
                    out float voxelCellSize))
            {
                return _cartographyTexture != null && !force;
            }

            PlayerExplorationTracker explorationTracker = GlobalRegistry.PlayerExploration;
            NativeArray<ulong> explorationWords = _emptyExplorationWords;
            int explorationAxisLength = 0;
            int explorationOriginOffset = 0;
            int explorationChunkSize = 0;
            byte hasExplorationMask = 0;
            if (explorationTracker != null)
            {
                if (explorationTracker.TryGetExplorationMaskPayload(
                    out explorationWords,
                    out explorationAxisLength,
                    out explorationOriginOffset,
                    out explorationChunkSize))
                {
                    hasExplorationMask = (byte)(explorationWords.IsCreated ? 1 : 0);
                    if (!explorationWords.IsCreated)
                        explorationWords = _emptyExplorationWords;
                }
                else
                {
                    explorationWords = _emptyExplorationWords;
                }
            }

            NativeArray<float> acousticDensity = _emptyAcousticDensity;
            int3 acousticDimensions = int3.zero;
            byte hasAcousticDensity = 0;
            if (WorldSpatialHashGrid.TryGetAcousticDensityMap(out acousticDensity, out Vector3Int acousticDimensionVector))
            {
                acousticDimensions = new int3(acousticDimensionVector.x, acousticDimensionVector.y, acousticDimensionVector.z);
                hasAcousticDensity = (byte)(acousticDensity.IsCreated ? 1 : 0);
                if (!acousticDensity.IsCreated)
                    acousticDensity = _emptyAcousticDensity;
            }
            else
            {
                acousticDensity = _emptyAcousticDensity;
            }

            _cartographyJobHandle = new BuildCartographyTextureJob
            {
                Passability = passability,
                ExplorationWords = explorationWords,
                AcousticDensity = acousticDensity,
                Pixels = _cartographyPixels,
                VoxelDimensions = voxelDimensions,
                VoxelOrigin = voxelOrigin,
                VoxelCellSize = math.max(0.001f, voxelCellSize),
                PlayerPosition = (float3)playerPosition,
                TextureSize = CartographyTextureSize,
                ExplorationAxisLength = explorationAxisLength,
                ExplorationOriginOffset = explorationOriginOffset,
                ExplorationChunkSizeMeters = explorationChunkSize,
                AcousticDimensions = acousticDimensions,
                AcousticRadiusMeters = AcousticOverlayRadiusMeters,
                SolidCell = VoxelDynamicNavGridRuntime.SolidCell,
                HasExplorationMask = hasExplorationMask,
                HasAcousticDensity = hasAcousticDensity
            }.Schedule(_cartographyPixels.Length, 64);

            _cartographyJobScheduled = true;
            return true;
        }

        private void EnsureCartographyResources()
        {
            if (_cartographyTexture == null)
            {
                _cartographyTexture = new Texture2D(
                    CartographyTextureSize,
                    CartographyTextureSize,
                    TextureFormat.RGBA32,
                    false,
                    true)
                {
                    name = "__PDAHeadlessCartography",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Point,
                    anisoLevel = 0
                }; // COLD ALLOC: Texture2D[128x128 RGBA32] — headless PDA cartography output — owner: PDAMapTab
            }

            if (!_cartographyPixels.IsCreated)
            {
                _cartographyPixels = new NativeArray<Color32>(
                    CartographyTextureSize * CartographyTextureSize,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<Color32>[16384] — headless PDA cartography pixel buffer — owner: PDAMapTab
                NativeMemorySentinel.RegisterNativeArray(
                    _cartographyPixels,
                    nameof(PDAMapTab),
                    nameof(_cartographyPixels),
                    NativeAllocationLifetime.Scene);
            }

            if (!_emptyExplorationWords.IsCreated)
            {
                _emptyExplorationWords = new NativeArray<ulong>(
                    1,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<ulong>[1] — created empty exploration-mask fallback for PDA cartography jobs — owner: PDAMapTab
                NativeMemorySentinel.RegisterNativeArray(
                    _emptyExplorationWords,
                    nameof(PDAMapTab),
                    nameof(_emptyExplorationWords),
                    NativeAllocationLifetime.Scene);
            }

            if (!_emptyAcousticDensity.IsCreated)
            {
                _emptyAcousticDensity = new NativeArray<float>(
                    1,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[1] — created empty acoustic-density fallback for PDA cartography jobs — owner: PDAMapTab
                NativeMemorySentinel.RegisterNativeArray(
                    _emptyAcousticDensity,
                    nameof(PDAMapTab),
                    nameof(_emptyAcousticDensity),
                    NativeAllocationLifetime.Scene);
            }
        }

        private void EnsurePointCloudResources()
        {
            if (_pointCloudAppendBuffer == null || !_pointCloudAppendBuffer.IsValid())
            {
                _pointCloudAppendBuffer = new GraphicsBuffer(
                    GraphicsBuffer.Target.Append,
                    PointCloudCapacity,
                    SonarPointStrideBytes); // COLD ALLOC: GraphicsBuffer[528 x 16B] — GPU-resident PDA sonar point cloud — owner: PDAMapTab
            }

            if (_pointCloudIndirectArgsBuffer == null || !_pointCloudIndirectArgsBuffer.IsValid())
            {
                _pointCloudIndirectArgsBuffer = new GraphicsBuffer(
                    GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Raw,
                    1,
                    SonarIndirectArgsStrideBytes); // COLD ALLOC: GraphicsBuffer[5 uint] - GPU-written PDA sonar indirect args - owner: PDAMapTab
            }

            if (SystemInfo.supportsSetConstantBuffer &&
                (_sonarMapConstantsBuffer == null || !_sonarMapConstantsBuffer.IsValid()))
            {
                _sonarMapConstantsBuffer = new GraphicsBuffer(
                    GraphicsBuffer.Target.Constant,
                    GraphicsBuffer.UsageFlags.LockBufferForWrite,
                    1,
                    SonarMapConstantsStrideBytes); // COLD ALLOC: GraphicsBuffer[96B] - packed PDA sonar compute constants - owner: PDAMapTab
            }

            if (_emptyPredatorAupBuffer == null || !_emptyPredatorAupBuffer.IsValid())
            {
                _emptyPredatorAupBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<Vector4>(1); // COLD ALLOC: GraphicsBuffer[1 x float4] - zero fallback predator AUP buffer - owner: PDAMapTab
                GraphicsBufferUploadUtility.UploadArray(_emptyPredatorAupBuffer, _emptyPredatorAupUpload, 1);
            }

            EnsurePointCloudQuadMesh();

            if (_pointCloudMaterial != null)
            {
                return;
            }

            if (!TryResolvePointCloudAssets())
                return;

            _pointCloudMaterial = new Material(sonarPointCloudShader)
            {
                name = "Runtime_PDASonarPointCloud"
            }; // COLD ALLOC: Material[1] — GPU-resident PDA sonar point-cloud draw material — owner: PDAMapTab
            _pointCloudMaterial.SetBuffer(SonarPointsId, _pointCloudAppendBuffer);
            TryResolveSonarComputeKernels();
        }

        private bool TryResolvePointCloudAssets()
        {
            if (_pointCloudAssetLookupAttempted)
                return sonarPointCloudShader != null;

            _pointCloudAssetLookupAttempted = true;

#if UNITY_EDITOR
            if (sonarPointCloudShader == null)
                sonarPointCloudShader = UnityEditor.AssetDatabase.LoadAssetAtPath<Shader>(SonarPointCloudShaderPath);
            if (sonarMapCompute == null)
                sonarMapCompute = UnityEditor.AssetDatabase.LoadAssetAtPath<ComputeShader>(SonarMapComputePath);
#endif
            if (sonarPointCloudShader == null)
                sonarPointCloudShader = Shader.Find(SonarPointCloudShaderName);

            return sonarPointCloudShader != null;
        }

        private void EnsurePointCloudQuadMesh()
        {
            if (_pointCloudQuadMesh != null)
                return;

            _pointCloudQuadMesh = new Mesh
            {
                name = "__PDASonarPointCloudIndirectQuad",
                bounds = new Bounds(Vector3.zero, Vector3.one * 2f)
            }; // COLD ALLOC: Mesh[1] - single quad used by DrawMeshInstancedIndirect PDA sonar point cloud - owner: PDAMapTab
            _pointCloudQuadMesh.vertices = SonarQuadVertices;
            _pointCloudQuadMesh.SetIndices(SonarQuadIndices, MeshTopology.Triangles, 0, false);
            _pointCloudQuadMesh.UploadMeshData(true);
        }

        private bool TryResolveSonarComputeKernels()
        {
            if (_sonarComputeKernelsResolved)
                return _sonarClearArgsKernel >= 0 && _sonarRaymarchKernel >= 0;

            if (sonarMapCompute == null)
                return false;

            if (!sonarMapCompute.HasKernel("CSClearArgs") ||
                !sonarMapCompute.HasKernel("CSRaymarch"))
            {
                return false;
            }

            _sonarClearArgsKernel = sonarMapCompute.FindKernel("CSClearArgs");
            _sonarRaymarchKernel = sonarMapCompute.FindKernel("CSRaymarch");
            _sonarComputeKernelsResolved = _sonarClearArgsKernel >= 0 &&
                                           _sonarRaymarchKernel >= 0 &&
                                           sonarMapCompute.IsSupported(_sonarClearArgsKernel) &&
                                           sonarMapCompute.IsSupported(_sonarRaymarchKernel);
            if (_sonarComputeKernelsResolved)
            {
                sonarMapCompute.GetKernelThreadGroupSizes(
                    _sonarRaymarchKernel,
                    out uint threadGroupSizeX,
                    out uint threadGroupSizeY,
                    out uint threadGroupSizeZ);
                _sonarRaymarchThreadGroupSizeX = threadGroupSizeX > 0u ? (int)threadGroupSizeX : PointCloudThreadAxis;
                _sonarRaymarchThreadGroupSizeY = threadGroupSizeY > 0u ? (int)threadGroupSizeY : PointCloudThreadAxis;
                _sonarRaymarchThreadGroupSizeZ = threadGroupSizeZ > 0u ? (int)threadGroupSizeZ : PointCloudThreadAxis;
            }

            return _sonarComputeKernelsResolved;
        }

        private void CompleteCartographyJobIfNeeded(bool applyTexture)
        {
            if (!_cartographyJobScheduled)
                return;

            if (!DispatcherJobSwap.TryFinalizeCompleted(ref _cartographyJobHandle))
                return;

            _cartographyJobScheduled = false;
            if (!applyTexture || _cartographyTexture == null || !_cartographyPixels.IsCreated)
                return;

            _cartographyTexture.SetPixelData(_cartographyPixels, 0);
            _cartographyTexture.Apply(false, false);
            if (mapImage != null)
            {
                mapImage.texture = _cartographyTexture;
                mapImage.material = null;
            }

        }

        private void RenderPointCloud()
        {
            if (mapImage == null || !isActiveAndEnabled || _sdfTexture == null || !_pointCloudSdfReady)
                return;

            EnsurePointCloudResources();
            if (_pointCloudMaterial == null ||
                _pointCloudAppendBuffer == null ||
                !_pointCloudAppendBuffer.IsValid() ||
                _pointCloudIndirectArgsBuffer == null ||
                !_pointCloudIndirectArgsBuffer.IsValid() ||
                _pointCloudQuadMesh == null ||
                !TryResolveSonarComputeKernels())
            {
                return;
            }

            if (!TryResolvePlayerRuntimePosition(out Vector3 playerPosition))
                return;

            if (!TryResolvePointCloudFrame(out Matrix4x4 localToWorld, out Bounds bounds, out Camera renderCamera))
                return;

            bool lowTier = ResolvePointCloudLowTier();
            if (!DispatchSonarPointCloud(playerPosition, lowTier))
                return;

            float pingRadius = math.frac(_animationTime * 0.33f) * 0.62f;
            _pointCloudMaterial.SetBuffer(SonarPointsId, _pointCloudAppendBuffer);
            _pointCloudMaterial.SetMatrix(PointCloudLocalToWorldId, localToWorld);
            _pointCloudMaterial.SetVector(AcousticPingSignalId, new Vector4(pingRadius, PointCloudPingBandWidth, _animationTime, 1f));
            _pointCloudMaterial.SetFloat(PointSizeId, pointCloudPointSize);
            _pointCloudMaterial.SetFloat(OpacityId, pointCloudOpacity);
            _pointCloudMaterial.SetFloat(DepthFadeMetersId, pointCloudDepthMeters);
            _pointCloudMaterial.SetFloat(HeightColorizationId, lowTier ? 0f : 1f);

            Graphics.DrawMeshInstancedIndirect(
                _pointCloudQuadMesh,
                0,
                _pointCloudMaterial,
                bounds,
                _pointCloudIndirectArgsBuffer,
                0,
                null,
                ShadowCastingMode.Off,
                false,
                gameObject.layer,
                renderCamera);
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

        private static int CeilDividePositive(int value, int divisor)
        {
            int safeDivisor = math.max(divisor, 1);
            return (value + safeDivisor - 1) / safeDivisor;
        }

        private bool DispatchSonarPointCloud(Vector3 playerPosition, bool lowTier)
        {
            if (sonarMapCompute == null ||
                _pointCloudAppendBuffer == null ||
                !_pointCloudAppendBuffer.IsValid() ||
                _pointCloudIndirectArgsBuffer == null ||
                !_pointCloudIndirectArgsBuffer.IsValid() ||
                _emptyPredatorAupBuffer == null ||
                !_emptyPredatorAupBuffer.IsValid() ||
                _sdfTexture == null ||
                !SystemInfo.supportsComputeShaders ||
                !TryResolveSonarComputeKernels())
            {
                return false;
            }

            int dispatchAxis = lowTier ? PointCloudLowAxis : PointCloudThreadAxis;
            int raymarchSteps = lowTier ? LowRaymarchSteps : HighRaymarchSteps;
            TryResolvePredatorAupBuffer(out GraphicsBuffer predatorAupBuffer, out int predatorAupCount);
            _pointCloudAppendBuffer.SetCounterValue(0u);
            UploadSonarMapConstants(playerPosition, dispatchAxis, raymarchSteps, predatorAupCount);

            sonarMapCompute.SetBuffer(_sonarClearArgsKernel, IndirectArgsId, _pointCloudIndirectArgsBuffer);
            sonarMapCompute.Dispatch(_sonarClearArgsKernel, 1, 1, 1);

            sonarMapCompute.SetTexture(_sonarRaymarchKernel, VoxelSdfTexture3DId, _sdfTexture);
            sonarMapCompute.SetBuffer(_sonarRaymarchKernel, SonarPointAppendBufferId, _pointCloudAppendBuffer);
            sonarMapCompute.SetBuffer(_sonarRaymarchKernel, PredatorAupBufferId, predatorAupBuffer);
            int groupsX = CeilDividePositive(dispatchAxis, _sonarRaymarchThreadGroupSizeX);
            int groupsY = CeilDividePositive(dispatchAxis, _sonarRaymarchThreadGroupSizeY);
            int groupsZ = CeilDividePositive(dispatchAxis, _sonarRaymarchThreadGroupSizeZ);
            sonarMapCompute.Dispatch(_sonarRaymarchKernel, groupsX, groupsY, groupsZ);
            GraphicsBuffer.CopyCount(_pointCloudAppendBuffer, _pointCloudIndirectArgsBuffer, sizeof(uint));
            return true;
        }

        private void UploadSonarMapConstants(Vector3 playerPosition, int dispatchAxis, int raymarchSteps, int predatorAupCount)
        {
            SonarMapConstants constants = new SonarMapConstants
            {
                GridDimensions = new Vector4(
                    _activeSdfGridDimensions.x,
                    _activeSdfGridDimensions.y,
                    _activeSdfGridDimensions.z,
                    0f),
                VolumeOrigin = new Vector4(
                    _activeSdfVolumeOrigin.x,
                    _activeSdfVolumeOrigin.y,
                    _activeSdfVolumeOrigin.z,
                    0f),
                VoxelCellSize = new Vector4(
                    _activeSdfVoxelCellSize.x,
                    _activeSdfVoxelCellSize.y,
                    _activeSdfVoxelCellSize.z,
                    0f),
                PlayerWorldPosition = new Vector4(
                    playerPosition.x,
                    playerPosition.y,
                    playerPosition.z,
                    0f),
                ScalarParams = new Vector4(
                    _activeSdfRange,
                    AcousticOverlayRadiusMeters,
                    _animationTime,
                    0f),
                DispatchParams = new Vector4(
                    dispatchAxis,
                    raymarchSteps,
                    predatorAupCount,
                    SonarQuadIndexCount)
            };

            if (SystemInfo.supportsSetConstantBuffer &&
                _sonarMapConstantsBuffer != null &&
                _sonarMapConstantsBuffer.IsValid())
            {
                _sonarMapConstantsUpload[0] = constants;
                GraphicsBufferUploadUtility.UploadArray(_sonarMapConstantsBuffer, _sonarMapConstantsUpload, 1);
                sonarMapCompute.SetConstantBuffer(SonarMapConstantsBufferName, _sonarMapConstantsBuffer, 0, SonarMapConstantsStrideBytes);
                return;
            }

            sonarMapCompute.SetVector(GridDimensionsId, constants.GridDimensions);
            sonarMapCompute.SetVector(VolumeOriginId, constants.VolumeOrigin);
            sonarMapCompute.SetVector(VoxelCellSizeId, constants.VoxelCellSize);
            sonarMapCompute.SetVector(PlayerWorldPositionId, constants.PlayerWorldPosition);
            sonarMapCompute.SetVector(SonarScalarParamsId, constants.ScalarParams);
            sonarMapCompute.SetVector(SonarDispatchParamsId, constants.DispatchParams);
        }

        private bool TryResolvePredatorAupBuffer(out GraphicsBuffer predatorAupBuffer, out int predatorAupCount)
        {
            predatorAupBuffer = _emptyPredatorAupBuffer;
            predatorAupCount = 0;

            IEncounterDirectorService encounterDirector = GlobalRegistry.EncounterDirector;
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

            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
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

        private bool ResolvePointCloudLowTier()
        {
            bool requestedLowTier = IsLowMathTierRequested();
            if (!_pointCloudTierInitialized)
            {
                _pointCloudTierInitialized = true;
                _pointCloudLowTierActive = requestedLowTier;
                _pointCloudLowTierCandidate = requestedLowTier;
                _pointCloudLowTierCandidateSince = _animationTime;
                return _pointCloudLowTierActive;
            }

            if (requestedLowTier == _pointCloudLowTierActive)
            {
                _pointCloudLowTierCandidate = requestedLowTier;
                _pointCloudLowTierCandidateSince = _animationTime;
                return _pointCloudLowTierActive;
            }

            if (requestedLowTier != _pointCloudLowTierCandidate)
            {
                _pointCloudLowTierCandidate = requestedLowTier;
                _pointCloudLowTierCandidateSince = _animationTime;
                return _pointCloudLowTierActive;
            }

            if (_animationTime - _pointCloudLowTierCandidateSince >= PointCloudTierHysteresisSeconds)
            {
                _pointCloudLowTierActive = requestedLowTier;
                _pointCloudLowTierCandidateSince = _animationTime;
            }

            return _pointCloudLowTierActive;
        }

        private static bool IsLowMathTierRequested()
        {
            HectonQualityTier tier = GlobalRegistry.ScalabilityTier;
            return HardwareTierDetector.SharedMemoryModeActive ||
                   tier == HectonQualityTier.Low ||
                   tier == HectonQualityTier.Mx350 ||
                   tier == HectonQualityTier.Unknown;
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
            PDAMarkerRegistry markerRegistry = GlobalRegistry.PDAMarkers;
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
            for (int i = 0; i < markerCount; i++)
            {
                uint markerHashId = _markerUpdateSnapshots[i].MarkerHashID;
                _markerUpdateSnapshots[i] = default;
                EnqueueMarkerUpdate(markerHashId);
            }
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

            EnsureMarkerOverlayBuilt();
            if (_markerOverlayRoot == null)
            {
                ClearPendingMarkerUpdates();
                return;
            }

            PDAMarkerRegistry markerRegistry = GlobalRegistry.PDAMarkers;
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

        private static bool TryResolveMarkerOverlayDelta(in PDAMarkerSnapshot marker, out float deltaX, out float deltaZ)
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

        private void EnsureSdfTexture(Vector3Int gridDimensions)
        {
            if (_sdfTexture != null &&
                _sdfTexture.width == gridDimensions.x &&
                _sdfTexture.height == gridDimensions.y &&
                _sdfTexture.depth == gridDimensions.z)
            {
                return;
            }

            if (_sdfTexture != null)
                Destroy(_sdfTexture);

            _sdfTexture = new Texture3D(
                gridDimensions.x,
                gridDimensions.y,
                gridDimensions.z,
                TextureFormat.R8,
                false)
            {
                name = "__PDASonarMapSdf",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 0
            }; // COLD ALLOC: Texture3D[1] — PDA sonar-map SDF volume texture — owner: PDAMapTab
        }

        private void RefreshThreatPings()
        {
            _activeThreatPingCount = 0;
            for (int pingIndex = 0; pingIndex < _threatPings.Length; pingIndex++)
                _threatPings[pingIndex] = Vector4.zero;

            if (WorldSpatialHashGrid.TryGetAcousticDensityMap(out NativeArray<float> densityMap, out Vector3Int densityDimensions))
            {
                RefreshThreatPingsFromSpatialDensity(densityMap, densityDimensions);
                TryAppendGhostSignalPing();
                RecountThreatPings();
                _threatPingsDirty = true;
                return;
            }

            Hecton8.Core.IAudioService audio = Hecton8.Core.GlobalRegistry.Audio;
            if (audio == null)
            {
                TryAppendGhostSignalPing();
                RecountThreatPings();
                _threatPingsDirty = true;
                return;
            }

            NativeArray<float> gridEnergy = default;
            int azimuthBins = 0;
            int elevationBins = 0;
            ComputeBuffer radarGridBuffer = null;
            if (!audio.TryGetAcousticRadarGridPayload(
                    out gridEnergy,
                    out azimuthBins,
                    out elevationBins,
                    out radarGridBuffer))
            {
                TryAppendGhostSignalPing();
                RecountThreatPings();
                _threatPingsDirty = true;
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
            _threatPingsDirty = true;
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

        private void RefreshThreatPingsFromSpatialDensity(NativeArray<float> densityMap, Vector3Int dimensions)
        {
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
            IWorldSeedProvider worldSeedProvider = GlobalRegistry.WorldSeedProvider;
            int seed = worldSeedProvider != null && worldSeedProvider.IsInitialized
                ? worldSeedProvider.RuntimeWorldSeed
                : 1;
            float unscaledTime = Time.unscaledTime;
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

        private void PushMaterialState()
        {
            if (_runtimeMapMaterial == null)
                return;

            _runtimeMapMaterial.SetFloat(TimePhaseId, _animationTime);
            if (!_threatPingsDirty && _appliedThreatPingCount == _activeThreatPingCount)
                return;

            _runtimeMapMaterial.SetInt(ThreatPingCountId, _activeThreatPingCount);
            _runtimeMapMaterial.SetVectorArray(ThreatPingsId, _threatPings);
            _appliedThreatPingCount = _activeThreatPingCount;
            _threatPingsDirty = false;
        }

        private static bool TryResolvePlayerRuntimePosition(out Vector3 playerPosition)
        {
            if (TryResolvePlayerAup(out AbsoluteUniversePosition playerAup))
            {
                playerPosition = playerAup.ToRuntimeFloat3();
                return true;
            }

            playerPosition = default;
            return false;
        }

        private static bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)
        {
            if (PlayerRuntimeContextService.TryGetActiveRuntimeContext(out PlayerRuntimeContext runtimeContext) &&
                runtimeContext != null)
            {
                PlayerMovementRuntimeState movementState = runtimeContext.MovementState;
                if ((movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u)
                {
                    playerAup = movementState.PredictedAup;
                    return true;
                }
            }

            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            HectonPlayerMovement playerMovement = playerContext != null ? playerContext.PlayerMovement : null;
            if (playerMovement != null)
            {
                playerAup = playerMovement.CurrentAup;
                return true;
            }

            playerAup = default;
            return false;
        }

        private static float ResolvePlayerDepthMeters()
        {
            BiomeMatrixDirector biomeMatrixDirector = BiomeMatrixDirector.ActiveRuntimeInstance;
            if (biomeMatrixDirector != null)
                return math.max(0f, biomeMatrixDirector.CurrentDepthMeters);

            if (!TryResolvePlayerAup(out AbsoluteUniversePosition playerAup))
                return 0f;

            double absoluteY = playerAup.ToAbsoluteDouble3().y;
            return (float)math.max(0d, -absoluteY);
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

        private static bool TryGetEmpBlindState(out TraumaDispatcher traumaDispatcher)
        {
            traumaDispatcher = null;
            if (!PlayerRuntimeContextService.TryGetActiveRuntimeContext(out PlayerRuntimeContext runtimeContext) ||
                runtimeContext == null)
            {
                return false;
            }

            traumaDispatcher = runtimeContext.TraumaDispatcher;
            return traumaDispatcher != null && traumaDispatcher.IsEmpSensorBlindActive;
        }

        private void ReleaseResources()
        {
            FinalizeNativeDisposeHandle();
            JobHandle disposeDependency = _cartographyJobScheduled ? _cartographyJobHandle : default;
            _cartographyJobScheduled = false;
            _cartographyJobHandle = default;

            if (_sdfTexture != null)
            {
                Destroy(_sdfTexture);
                _sdfTexture = null;
            }

            if (_cartographyTexture != null)
            {
                Destroy(_cartographyTexture);
                _cartographyTexture = null;
            }

            disposeDependency = DisposeNativeArray(ref _cartographyPixels, disposeDependency);
            disposeDependency = DisposeNativeArray(ref _emptyExplorationWords, disposeDependency);
            disposeDependency = DisposeNativeArray(ref _emptyAcousticDensity, disposeDependency);
            _nativeDisposeHandle = JobHandle.CombineDependencies(_nativeDisposeHandle, disposeDependency);

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

            if (_sonarMapConstantsBuffer != null)
            {
                _sonarMapConstantsBuffer.Release();
                _sonarMapConstantsBuffer = null;
            }

            if (_emptyPredatorAupBuffer != null)
            {
                _emptyPredatorAupBuffer.Release();
                _emptyPredatorAupBuffer = null;
            }

            if (_pointCloudQuadMesh != null)
            {
                Destroy(_pointCloudQuadMesh);
                _pointCloudQuadMesh = null;
            }

            if (_pointCloudMaterial != null)
            {
                Destroy(_pointCloudMaterial);
                _pointCloudMaterial = null;
            }

            if (_runtimeMapMaterial != null)
            {
                Destroy(_runtimeMapMaterial);
                _runtimeMapMaterial = null;
            }

            _sonarClearArgsKernel = -1;
            _sonarRaymarchKernel = -1;
            _sonarRaymarchThreadGroupSizeX = PointCloudThreadAxis;
            _sonarRaymarchThreadGroupSizeY = PointCloudThreadAxis;
            _sonarRaymarchThreadGroupSizeZ = PointCloudThreadAxis;
            _sonarComputeKernelsResolved = false;
            _pointCloudAssetLookupAttempted = false;
            _pointCloudSdfReady = false;
            _pointCloudTierInitialized = false;
            _pointCloudLowTierActive = false;
            _pointCloudLowTierCandidate = false;
            _pointCloudLowTierCandidateSince = 0f;
        }

        private static JobHandle DisposeNativeArray<T>(ref NativeArray<T> array, JobHandle dependency)
            where T : struct
        {
            if (!array.IsCreated)
                return dependency;

            NativeMemorySentinel.UnregisterNativeArray(array);
            JobHandle disposeHandle = array.Dispose(dependency);
            array = default;
            return disposeHandle;
        }

        private void FinalizeNativeDisposeHandle()
        {
            DispatcherJobSwap.TryFinalizeCompleted(ref _nativeDisposeHandle);
        }
    }
}


