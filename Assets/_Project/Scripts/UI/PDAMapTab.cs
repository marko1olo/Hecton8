using System;
using System.Runtime.InteropServices;
using Hecton8.Audio;
using Hecton8.Bootstrap;
using Hecton8.Caves;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.PDA;
using Hecton8.World;
using TMPro;
using Unity.Burst;
using Unity.Collections;
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
    public sealed class PDAMapTab : MonoBehaviour, ITickable, IUpdatable, ILateFrameTickable
    {
        private const string SonarMapShaderPath = "Assets/_Project/Art/Shaders/Hecton_PDA_SonarMap.shader";
        private const string SonarPointCloudShaderPath = "Assets/_Project/Art/Shaders/Hecton_PDA_SonarPointCloud.shader";
        private const int MaxThreatPings = 8;
        private const int MaxStatusChars = 64;
        private static readonly bool UseHeadlessCartography = true;
        private const int CartographyTextureSize = 128;
        private const float AcousticOverlayRadiusMeters = 160f;
        private const int PointCloudAxis = 16;
        private const int PointCloudCapacity = PointCloudAxis * PointCloudAxis * PointCloudAxis;
        private const int SonarPointStrideBytes = 32;

        private static readonly int SdfVolumeId = Shader.PropertyToID("_SdfVolume");
        private static readonly int SdfRangeId = Shader.PropertyToID("_SdfRange");
        private static readonly int GridDimensionsId = Shader.PropertyToID("_GridDimensions");
        private static readonly int VolumeHalfExtentId = Shader.PropertyToID("_VolumeHalfExtent");
        private static readonly int ThreatPingCountId = Shader.PropertyToID("_ThreatPingCount");
        private static readonly int ThreatPingsId = Shader.PropertyToID("_ThreatPings");
        private static readonly int TimePhaseId = Shader.PropertyToID("_TimePhase");
        private static readonly int SonarPointsId = Shader.PropertyToID("_SonarPoints");
        private static readonly int PointCloudLocalToWorldId = Shader.PropertyToID("_PointCloudLocalToWorld");
        private static readonly int PointSizeId = Shader.PropertyToID("_PointSize");
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");

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

        [StructLayout(LayoutKind.Sequential)]
        private struct SonarPointCloudPoint
        {
            public float4 LocalPositionIntensity;
            public float4 Color;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct BuildSonarPointCloudJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<byte> Passability;
            [ReadOnly] public NativeArray<float> AcousticDensity;
            [WriteOnly] public NativeArray<SonarPointCloudPoint> Points;
            public int3 VoxelDimensions;
            public float3 VoxelOrigin;
            public float VoxelCellSize;
            public float3 PlayerPosition;
            public int3 AcousticDimensions;
            public float AcousticRadiusMeters;
            public byte SolidCell;
            public byte HasAcousticDensity;

            public void Execute(int index)
            {
                int sx = index % PointCloudAxis;
                int sy = (index / PointCloudAxis) % PointCloudAxis;
                int sz = index / (PointCloudAxis * PointCloudAxis);
                float3 sample01 = new float3(
                    (sx + 0.5f) / PointCloudAxis,
                    (sy + 0.5f) / PointCloudAxis,
                    (sz + 0.5f) / PointCloudAxis);

                int vx = math.clamp((int)math.floor(sample01.x * VoxelDimensions.x), 0, math.max(0, VoxelDimensions.x - 1));
                int vy = math.clamp((int)math.floor(sample01.y * VoxelDimensions.y), 0, math.max(0, VoxelDimensions.y - 1));
                int vz = math.clamp((int)math.floor(sample01.z * VoxelDimensions.z), 0, math.max(0, VoxelDimensions.z - 1));
                int voxelIndex = vx + (vy * VoxelDimensions.x) + (vz * VoxelDimensions.x * VoxelDimensions.y);
                float3 worldPosition = VoxelOrigin + new float3(vx, vy, vz) * math.max(0.001f, VoxelCellSize);
                float acoustic = SampleAcoustic(worldPosition);
                bool solid = (uint)voxelIndex < (uint)Passability.Length && Passability[voxelIndex] == SolidCell;
                float visibility = solid ? 0.72f : acoustic;
                if (!solid && acoustic <= 0.01f)
                    visibility = 0f;

                float3 localPosition = math.clamp((worldPosition - PlayerPosition) / math.max(1f, AcousticRadiusMeters), -0.5f, 0.5f);
                float3 baseColor = solid
                    ? new float3(0.22f, 0.95f, 1f)
                    : new float3(1f, 0.24f, 0.12f);
                float3 acousticColor = math.lerp(baseColor, new float3(1f, 0.55f, 0.08f), math.saturate(acoustic));

                Points[index] = new SonarPointCloudPoint
                {
                    LocalPositionIntensity = new float4(localPosition, visibility),
                    Color = new float4(acousticColor, math.saturate(visibility))
                };
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
        }

        [Header("References")]
        [SerializeField, Tooltip("Optional explicit raymarched-map shader. Editor fallback resolves the first-party asset path when left null.")]
        private Shader sonarMapShader;
        [SerializeField, Tooltip("Optional explicit GPU point-cloud shader. Editor fallback resolves the first-party asset path when left null.")]
        private Shader sonarPointCloudShader;
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

        private readonly Vector4[] _threatPings = new Vector4[MaxThreatPings]; // COLD ALLOC: Vector4[8] - PDA sonar-map threat ping upload cache - owner: PDAMapTab
        private bool _registered;
        private bool _registeredLateFrame;
        private float _refreshCountdown;
        private float _animationTime;
        private int _activeVolumeVersion = -1;
        private int _activeThreatPingCount;
        private Texture3D _sdfTexture;
        private Texture2D _cartographyTexture;
        private NativeArray<Color32> _cartographyPixels;
        private NativeArray<ulong> _emptyExplorationWords;
        private NativeArray<float> _emptyAcousticDensity;
        private NativeArray<SonarPointCloudPoint> _pointCloudPoints;
        private JobHandle _cartographyJobHandle;
        private bool _cartographyJobScheduled;
        private bool _pointCloudUploadPending;
        private int _pointCloudVertexCount;
        private GraphicsBuffer _pointCloudBuffer;
        private Material _pointCloudMaterial;
        private Material _runtimeMapMaterial;
        private HectonVoxelVolume _activeVolume;
        private CharBufferPool.Lease _statusBufferLease;
        private readonly Vector3[] _mapWorldCorners = new Vector3[4]; // COLD ALLOC: Vector3[4] - PDA map point-cloud basis corners - owner: PDAMapTab

        private void Awake()
        {
            EnsureBuilt();
        }

        private void OnEnable()
        {
            EnsureBuilt();
            TryAcquireStatusBuffer();
            RegisterToTickManager();
            RefreshMapSource(force: true);
        }

        private void OnDisable()
        {
            UnregisterFromTickManager();
            CompleteCartographyJobIfNeeded(applyTexture: false);
            ReleaseStatusBuffer();
        }

        private void OnDestroy()
        {
            UnregisterFromTickManager();
            CompleteCartographyJobIfNeeded(applyTexture: false);
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
                _refreshCountdown = Mathf.Max(0.05f, sourceRefreshInterval);
                RefreshMapSource(force: false);
            }

            PushMaterialState();
        }

        /// <summary>
        /// Applies completed headless cartography jobs during the dispatcher LateUpdate lane.
        /// </summary>
        public void LateFrameTick()
        {
            CompleteCartographyJobIfNeeded(applyTexture: true);
            RenderPointCloud();
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

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.UI);
            _registered = true;
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

            GlobalRegistry.RegisterLateFrameTickable(this, PriorityLayer.UI);
            _registeredLateFrame = true;
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

                GameObject imageOwner = new GameObject("MapImage", typeof(RectTransform));
                imageOwner.layer = gameObject.layer;
                RectTransform imageRect = imageOwner.GetComponent<RectTransform>();
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

            if (statusLabel == null)
            {
                GameObject statusOwner = new GameObject("MapStatus", typeof(RectTransform));
                statusOwner.layer = gameObject.layer;
                RectTransform statusRect = statusOwner.GetComponent<RectTransform>();
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
                    }; // COLD ALLOC: Material[1] - diegetic PDA sonar-map raymarch material - owner: PDAMapTab
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
            GameObject owner = new GameObject(name, typeof(RectTransform));
            owner.layer = parent.gameObject.layer;
            RectTransform rect = owner.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private void RefreshMapSource(bool force)
        {
            if (TryGetEmpBlindState(out _))
            {
                _activeVolume = null;
                _activeVolumeVersion = -1;
                _activeThreatPingCount = 0;
                WriteEmpBlindStatus();
                return;
            }

            Vector3 playerPosition = ResolvePlayerPosition();
            if (UseHeadlessCartography)
            {
                if (!ScheduleHeadlessCartography(playerPosition, force))
                {
                    _activeVolume = null;
                    _activeVolumeVersion = -1;
                    _activeThreatPingCount = 0;
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
                _activeThreatPingCount = 0;
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
                _activeThreatPingCount = 0;
                WriteOfflineStatus();
                return;
            }

            bool sourceChanged = force ||
                                 !ReferenceEquals(_activeVolume, volume) ||
                                 _activeVolumeVersion != version;
            _activeVolume = volume;
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

            PlayerExplorationTracker explorationTracker = PlayerExplorationTracker.Instance;
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

            EnsurePointCloudResources();
            if (_pointCloudPoints.IsCreated)
            {
                JobHandle pointCloudHandle = new BuildSonarPointCloudJob
                {
                    Passability = passability,
                    AcousticDensity = acousticDensity,
                    Points = _pointCloudPoints,
                    VoxelDimensions = voxelDimensions,
                    VoxelOrigin = voxelOrigin,
                    VoxelCellSize = math.max(0.001f, voxelCellSize),
                    PlayerPosition = (float3)playerPosition,
                    AcousticDimensions = acousticDimensions,
                    AcousticRadiusMeters = AcousticOverlayRadiusMeters,
                    SolidCell = VoxelDynamicNavGridRuntime.SolidCell,
                    HasAcousticDensity = hasAcousticDensity
                }.Schedule(_pointCloudPoints.Length, 64);
                _cartographyJobHandle = JobHandle.CombineDependencies(_cartographyJobHandle, pointCloudHandle);
                _pointCloudVertexCount = _pointCloudPoints.Length;
                _pointCloudUploadPending = true;
            }

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
                }; // COLD ALLOC: Texture2D[128x128 RGBA32] - headless PDA cartography output - owner: PDAMapTab
            }

            if (!_cartographyPixels.IsCreated)
            {
                _cartographyPixels = new NativeArray<Color32>(
                    CartographyTextureSize * CartographyTextureSize,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<Color32>[16384] - headless PDA cartography pixel buffer - owner: PDAMapTab
            }

            if (!_emptyExplorationWords.IsCreated)
            {
                _emptyExplorationWords = new NativeArray<ulong>(
                    1,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<ulong>[1] - created empty exploration-mask fallback for PDA cartography jobs - owner: PDAMapTab
            }

            if (!_emptyAcousticDensity.IsCreated)
            {
                _emptyAcousticDensity = new NativeArray<float>(
                    1,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[1] - created empty acoustic-density fallback for PDA cartography jobs - owner: PDAMapTab
            }
        }

        private void EnsurePointCloudResources()
        {
            if (!_pointCloudPoints.IsCreated)
            {
                _pointCloudPoints = new NativeArray<SonarPointCloudPoint>(
                    PointCloudCapacity,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<SonarPointCloudPoint>[4096] - PDA sonar point-cloud upload payload - owner: PDAMapTab
            }

            if (_pointCloudBuffer == null || !_pointCloudBuffer.IsValid())
            {
                _pointCloudBuffer = new GraphicsBuffer(
                    GraphicsBuffer.Target.Structured,
                    PointCloudCapacity,
                    SonarPointStrideBytes); // COLD ALLOC: GraphicsBuffer[4096 x 32B] - GPU-resident PDA sonar point cloud - owner: PDAMapTab
            }

            if (_pointCloudMaterial != null)
                return;

#if UNITY_EDITOR
            if (sonarPointCloudShader == null)
                sonarPointCloudShader = UnityEditor.AssetDatabase.LoadAssetAtPath<Shader>(SonarPointCloudShaderPath);
#endif
            if (sonarPointCloudShader == null)
                sonarPointCloudShader = Shader.Find("Hecton8/UI/PDA Sonar Point Cloud");

            if (sonarPointCloudShader == null)
                return;

            _pointCloudMaterial = new Material(sonarPointCloudShader)
            {
                name = "Runtime_PDASonarPointCloud"
            }; // COLD ALLOC: Material[1] - GPU-resident PDA sonar point-cloud draw material - owner: PDAMapTab
            _pointCloudMaterial.SetBuffer(SonarPointsId, _pointCloudBuffer);
        }

        private void CompleteCartographyJobIfNeeded(bool applyTexture)
        {
            if (!_cartographyJobScheduled)
                return;

            if (applyTexture && !_cartographyJobHandle.IsCompleted)
                return;

            _cartographyJobHandle.Complete();
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

            UploadPointCloudIfNeeded();
        }

        private void UploadPointCloudIfNeeded()
        {
            if (!_pointCloudUploadPending ||
                _pointCloudBuffer == null ||
                !_pointCloudBuffer.IsValid() ||
                !_pointCloudPoints.IsCreated)
            {
                return;
            }

            _pointCloudBuffer.SetData(_pointCloudPoints, 0, 0, _pointCloudPoints.Length);
            _pointCloudUploadPending = false;
        }

        private void RenderPointCloud()
        {
            if (_pointCloudVertexCount <= 0 ||
                _pointCloudMaterial == null ||
                _pointCloudBuffer == null ||
                !_pointCloudBuffer.IsValid() ||
                mapImage == null ||
                !isActiveAndEnabled)
            {
                return;
            }

            RectTransform mapRect = mapImage.rectTransform;
            if (mapRect == null)
                return;

            mapRect.GetWorldCorners(_mapWorldCorners);
            Vector3 bottomLeft = _mapWorldCorners[0];
            Vector3 topLeft = _mapWorldCorners[1];
            Vector3 topRight = _mapWorldCorners[2];
            Vector3 bottomRight = _mapWorldCorners[3];
            Vector3 right = bottomRight - bottomLeft;
            Vector3 up = topLeft - bottomLeft;
            Vector3 center = (bottomLeft + topLeft + topRight + bottomRight) * 0.25f;
            Vector3 normal = Vector3.Cross(right, up);
            if (normal.sqrMagnitude < 0.000001f)
                return;

            normal.Normalize();
            Matrix4x4 localToWorld = Matrix4x4.identity;
            localToWorld.SetColumn(0, new Vector4(right.x, right.y, right.z, 0f));
            localToWorld.SetColumn(1, new Vector4(up.x, up.y, up.z, 0f));
            localToWorld.SetColumn(2, new Vector4(normal.x * pointCloudDepthMeters, normal.y * pointCloudDepthMeters, normal.z * pointCloudDepthMeters, 0f));
            localToWorld.SetColumn(3, new Vector4(center.x, center.y, center.z, 1f));

            _pointCloudMaterial.SetBuffer(SonarPointsId, _pointCloudBuffer);
            _pointCloudMaterial.SetMatrix(PointCloudLocalToWorldId, localToWorld);
            _pointCloudMaterial.SetFloat(PointSizeId, pointCloudPointSize);
            _pointCloudMaterial.SetFloat(OpacityId, pointCloudOpacity);

            Bounds bounds = new Bounds(center, new Vector3(right.magnitude, up.magnitude, pointCloudDepthMeters + 0.01f));
            RenderParams renderParams = new RenderParams(_pointCloudMaterial)
            {
                worldBounds = bounds,
                layer = gameObject.layer,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false
            };
            Graphics.RenderPrimitives(renderParams, MeshTopology.Points, _pointCloudVertexCount);
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
            }; // COLD ALLOC: Texture3D[1] - PDA sonar-map SDF volume texture - owner: PDAMapTab
        }

        private void RefreshThreatPings()
        {
            _activeThreatPingCount = 0;
            for (int pingIndex = 0; pingIndex < _threatPings.Length; pingIndex++)
                _threatPings[pingIndex] = Vector4.zero;

            if (WorldSpatialHashGrid.TryGetAcousticDensityMap(out NativeArray<float> densityMap, out Vector3Int densityDimensions))
            {
                RefreshThreatPingsFromSpatialDensity(densityMap, densityDimensions);
                return;
            }

            Hecton8.Core.IAudioService audio = Hecton8.Core.GlobalRegistry.Audio;
            if (audio == null)
            {
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
                return;
            }

            for (int cellIndex = 0; cellIndex < gridEnergy.Length; cellIndex++)
            {
                float intensity = gridEnergy[cellIndex];
                if (intensity <= 0.025f)
                    continue;

                int weakestIndex = -1;
                float weakestIntensity = float.PositiveInfinity;
                for (int existingIndex = 0; existingIndex < MaxThreatPings; existingIndex++)
                {
                    float existingIntensity = _threatPings[existingIndex].w;
                    if (existingIntensity < weakestIntensity)
                    {
                        weakestIntensity = existingIntensity;
                        weakestIndex = existingIndex;
                    }
                }

                if (weakestIndex < 0 || intensity <= weakestIntensity)
                    continue;

                int azimuthIndex = cellIndex % azimuthBins;
                int elevationIndex = cellIndex / azimuthBins;
                float azimuthRadians = ((azimuthIndex + 0.5f) / Mathf.Max(1, azimuthBins)) * Mathf.PI * 2f;
                float elevation01 = (elevationIndex + 0.5f) / Mathf.Max(1, elevationBins);
                float elevationRadians = Mathf.Lerp(-Mathf.PI * 0.25f, Mathf.PI * 0.25f, elevation01);
                float cosElevation = Mathf.Cos(elevationRadians);

                Vector3 localPosition = new Vector3(
                    Mathf.Sin(azimuthRadians) * cosElevation,
                    Mathf.Sin(elevationRadians),
                    Mathf.Cos(azimuthRadians) * cosElevation) * 0.38f;
                _threatPings[weakestIndex] = new Vector4(
                    localPosition.x,
                    localPosition.y,
                    localPosition.z,
                    Mathf.Clamp01(intensity));
            }

            for (int i = 0; i < MaxThreatPings; i++)
            {
                if (_threatPings[i].w > 0f)
                    _activeThreatPingCount++;
            }
        }

        private void RefreshThreatPingsFromSpatialDensity(NativeArray<float> densityMap, Vector3Int dimensions)
        {
            int safeWidth = Mathf.Max(1, dimensions.x);
            int safeHeight = Mathf.Max(1, dimensions.y);
            int safeDepth = Mathf.Max(1, dimensions.z);
            int maxCells = Mathf.Min(densityMap.Length, safeWidth * safeHeight * safeDepth);
            for (int cellIndex = 0; cellIndex < maxCells; cellIndex++)
            {
                float intensity = densityMap[cellIndex];
                if (intensity <= 0.025f)
                    continue;

                int weakestIndex = -1;
                float weakestIntensity = float.PositiveInfinity;
                for (int existingIndex = 0; existingIndex < MaxThreatPings; existingIndex++)
                {
                    float existingIntensity = _threatPings[existingIndex].w;
                    if (existingIntensity < weakestIntensity)
                    {
                        weakestIntensity = existingIntensity;
                        weakestIndex = existingIndex;
                    }
                }

                if (weakestIndex < 0 || intensity <= weakestIntensity)
                    continue;

                int z = cellIndex / (safeWidth * safeHeight);
                int y = (cellIndex - (z * safeWidth * safeHeight)) / safeWidth;
                int x = cellIndex - (z * safeWidth * safeHeight) - (y * safeWidth);
                Vector3 localPosition = new Vector3(
                    ((x + 0.5f) / safeWidth) - 0.5f,
                    ((y + 0.5f) / safeHeight) - 0.5f,
                    ((z + 0.5f) / safeDepth) - 0.5f) * 0.76f;
                _threatPings[weakestIndex] = new Vector4(
                    localPosition.x,
                    localPosition.y,
                    localPosition.z,
                    Mathf.Clamp01(intensity));
            }

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
            _runtimeMapMaterial.SetInt(ThreatPingCountId, _activeThreatPingCount);
            _runtimeMapMaterial.SetVectorArray(ThreatPingsId, _threatPings);
        }

        private static Vector3 ResolvePlayerPosition()
        {
            if (SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform) && playerTransform != null)
                return playerTransform.position;

            return Vector3.zero;
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

            int safeLength = Mathf.Min(literal.Length, buffer.Length - offset);
            literal.AsSpan(0, safeLength).CopyTo(buffer.AsSpan(offset, safeLength));
            return safeLength;
        }

        private static int CopyLiteral(Span<char> buffer, int offset, string literal)
        {
            if (string.IsNullOrEmpty(literal) || offset >= buffer.Length)
                return 0;

            int safeLength = Mathf.Min(literal.Length, buffer.Length - offset);
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
            float worldSizeX = Mathf.Max(1, gridDimensions.x - 1) * Mathf.Max(0.0001f, voxelCellSize.x);
            float worldSizeY = Mathf.Max(1, gridDimensions.y - 1) * Mathf.Max(0.0001f, voxelCellSize.y);
            float worldSizeZ = Mathf.Max(1, gridDimensions.z - 1) * Mathf.Max(0.0001f, voxelCellSize.z);
            Vector3 worldHalfExtent = new Vector3(worldSizeX, worldSizeY, worldSizeZ) * 0.5f;
            float dominantHalfExtent = Mathf.Max(0.0001f, Mathf.Max(worldHalfExtent.x, Mathf.Max(worldHalfExtent.y, worldHalfExtent.z)));
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

            if (_cartographyPixels.IsCreated)
            {
                _cartographyPixels.Dispose();
                _cartographyPixels = default;
            }

            if (_emptyExplorationWords.IsCreated)
            {
                _emptyExplorationWords.Dispose();
                _emptyExplorationWords = default;
            }

            if (_emptyAcousticDensity.IsCreated)
            {
                _emptyAcousticDensity.Dispose();
                _emptyAcousticDensity = default;
            }

            if (_pointCloudPoints.IsCreated)
            {
                _pointCloudPoints.Dispose();
                _pointCloudPoints = default;
            }

            if (_pointCloudBuffer != null)
            {
                _pointCloudBuffer.Release();
                _pointCloudBuffer = null;
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

            _pointCloudUploadPending = false;
            _pointCloudVertexCount = 0;
        }
    }
}


