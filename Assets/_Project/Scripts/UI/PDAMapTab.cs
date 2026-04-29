using System;
using Hecton8.Audio;
using Hecton8.Bootstrap;
using Hecton8.Caves;
using Hecton8.Core;
using TMPro;
using Unity.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Hecton8.UI
{
    /// <summary>
    /// Diegetic PDA sonar-map viewport driven by the published cave SDF snapshot and acoustic threat grid.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/PDA Map Tab")]
    public sealed class PDAMapTab : MonoBehaviour, ITickable, IUpdatable
    {
        private const string SonarMapShaderPath = "Assets/_Project/Art/Shaders/Hecton_PDA_SonarMap.shader";
        private const int MaxThreatPings = 8;
        private const int MaxStatusChars = 64;

        private static readonly int SdfVolumeId = Shader.PropertyToID("_SdfVolume");
        private static readonly int SdfRangeId = Shader.PropertyToID("_SdfRange");
        private static readonly int GridDimensionsId = Shader.PropertyToID("_GridDimensions");
        private static readonly int ThreatPingCountId = Shader.PropertyToID("_ThreatPingCount");
        private static readonly int ThreatPingsId = Shader.PropertyToID("_ThreatPings");
        private static readonly int TimePhaseId = Shader.PropertyToID("_TimePhase");

        [Header("References")]
        [SerializeField, Tooltip("Optional explicit raymarched-map shader. Editor fallback resolves the first-party asset path when left null.")]
        private Shader sonarMapShader;
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
        [SerializeField, Range(0.05f, 2f), Tooltip("Seconds between sonar-source refreshes while the PDA tab remains open.")]
        private float sourceRefreshInterval = 0.2f;

        private readonly Vector4[] _threatPings = new Vector4[MaxThreatPings]; // COLD ALLOC: Vector4[8] - PDA sonar-map threat ping upload cache - owner: PDAMapTab
        private bool _registered;
        private float _refreshCountdown;
        private float _animationTime;
        private int _activeVolumeVersion = -1;
        private int _activeThreatPingCount;
        private Texture3D _sdfTexture;
        private Material _runtimeMapMaterial;
        private HectonVoxelVolume _activeVolume;
        private CharBufferPool.Lease _statusBufferLease;

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
            ReleaseStatusBuffer();
        }

        private void OnDestroy()
        {
            UnregisterFromTickManager();
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

        private void RegisterToTickManager()
        {
            if (_registered)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.UI);
            _registered = true;
        }

        private void UnregisterFromTickManager()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
            _registered = false;
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
            Vector3 playerPosition = ResolvePlayerPosition();
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
                }
            }

            RefreshThreatPings();
            WriteOnlineStatus(gridDimensions);
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

            for (int pingIndex = 0; pingIndex < _threatPings.Length; pingIndex++)
                _threatPings[pingIndex] = Vector4.zero;

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
            int length = CopyLiteral(buffer, 0, "SONAR MAP // OFFLINE");
            statusLabel.SetCharArray(buffer, 0, length);
        }

        private void WriteOnlineStatus(Vector3Int gridDimensions)
        {
            if (statusLabel == null || !_statusBufferLease.IsValid)
                return;

            Span<char> span = _statusBufferLease.Buffer.AsSpan();
            int cursor = 0;
            cursor += CopyLiteral(span, cursor, "SONAR ");
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

        private void ReleaseResources()
        {
            if (_sdfTexture != null)
            {
                Destroy(_sdfTexture);
                _sdfTexture = null;
            }

            if (_runtimeMapMaterial != null)
            {
                Destroy(_runtimeMapMaterial);
                _runtimeMapMaterial = null;
            }
        }
    }
}


